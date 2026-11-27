using UnityEngine;
using UnityEngine.Rendering;

namespace RobotArena
{
    /// <summary>
    /// Real 3D pieces thrown off things that break: concrete lumps and roof tiles from a
    /// house, splinters and leaves from a tree, paint panels and glass from a car. Every
    /// piece takes its colour from the object it came off, so nothing out of place flies
    /// out. All pieces live in one dynamic mesh (one draw call), fly ballistically, bounce
    /// on the ground, settle and after a while sink away.
    /// </summary>
    public class Rubble : MonoBehaviour
    {
        public enum Shape : byte { Chunk, Slab, Splinter, Leaf, Glass }

        const int Max = 420;        // pieces alive at once
        const int V = 24;           // vertices per piece (a deformed box, 6 faces)
        const int T = 36;           // indices per piece

        struct Piece
        {
            public Vector3 pos, vel, spin;
            public Quaternion rot;
            public Vector3 scale;
            public float age, life, drag, gravity;
            public byte tpl;
            public bool alive, resting;
        }

        static Rubble instance;

        Mesh mesh;
        readonly Piece[] pieces = new Piece[Max];
        Vector3[] verts, norms;
        Color32[] cols;
        int next, active;
        bool colsDirty;

        // Template corner sets (unit size) and their per-vertex positions/normals.
        static Vector3[][] tplVerts, tplNorms;
        static readonly int[] tplOfShape = { 0, 3, 5, 6, 7 };   // first template per shape
        static readonly int[] tplCount = { 3, 2, 1, 1, 1 };

        /// <summary>Creates the debris system for the battle (called by WorldDetail).</summary>
        public static void Create(Material mat)
        {
            if (instance != null) return;
            var go = new GameObject("Rubble");
            instance = go.AddComponent<Rubble>();
            instance.Init(mat);
        }

        void OnDestroy() { if (instance == this) instance = null; }

        void Init(Material mat)
        {
            BuildTemplates();
            verts = new Vector3[Max * V];
            norms = new Vector3[Max * V];
            cols = new Color32[Max * V];
            var tris = new int[Max * T];
            for (int p = 0; p < Max; p++)
                for (int f = 0; f < 6; f++)
                {
                    int vb = p * V + f * 4, tb = p * T + f * 6;
                    tris[tb] = vb; tris[tb + 1] = vb + 1; tris[tb + 2] = vb + 2;
                    tris[tb + 3] = vb; tris[tb + 4] = vb + 2; tris[tb + 5] = vb + 3;
                }
            mesh = new Mesh { name = "Rubble", indexFormat = IndexFormat.UInt16 };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.colors32 = cols;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4000f);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            bool high = GameSettings.GraphicsQuality != GameSettings.Quality.Low;
            r.shadowCastingMode = high ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
        }

        // ---------------------------------------------------------------------
        // Spawning
        // ---------------------------------------------------------------------

        /// <summary>Throws one piece.</summary>
        public static void Spawn(Shape shape, Vector3 pos, Vector3 vel, Vector3 size, Color32 col, float life = 0f)
        {
            if (instance == null) return;
            instance.Add(shape, pos, vel, size, col, life);
        }

        /// <summary>
        /// Throws count pieces out of an area, coloured from the given palette (the
        /// broken object's own colours).
        /// </summary>
        public static void Burst(Shape shape, Vector3 centre, Vector3 area, int count, Color32[] palette, int paletteCount,
            float sizeMin, float sizeMax, float speed, Vector3 push, float upward = 0.6f)
        {
            if (instance == null || paletteCount <= 0) return;
            if (GameSettings.GraphicsQuality == GameSettings.Quality.Low) count = (count + 1) / 2;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = centre + Vector3.Scale(Random.insideUnitSphere, area);
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * upward + (1f - upward) * dir.y;
                Vector3 v = dir * speed * Random.Range(0.35f, 1f) + push;
                float s = Random.Range(sizeMin, sizeMax);
                Vector3 size;
                switch (shape)
                {
                    case Shape.Slab: size = new Vector3(s, s * Random.Range(0.12f, 0.22f), s * Random.Range(0.6f, 1f)); break;
                    case Shape.Splinter: size = new Vector3(s * Random.Range(0.12f, 0.2f), s * Random.Range(0.12f, 0.2f), s); break;
                    case Shape.Leaf: size = new Vector3(s, s * 0.05f, s * Random.Range(0.6f, 0.9f)); break;
                    case Shape.Glass: size = new Vector3(s, s * 0.04f, s * Random.Range(0.5f, 1f)); break;
                    default: size = new Vector3(s, s * Random.Range(0.55f, 0.9f), s * Random.Range(0.6f, 1f)); break;
                }
                Color32 c = palette[Random.Range(0, paletteCount)];
                float k = Random.Range(0.8f, 1.05f);
                c = new Color32((byte)Mathf.Min(255, c.r * k), (byte)Mathf.Min(255, c.g * k), (byte)Mathf.Min(255, c.b * k), c.a);
                instance.Add(shape, p, v, size, c, 0f);
            }
        }

        void Add(Shape shape, Vector3 pos, Vector3 vel, Vector3 size, Color32 col, float life)
        {
            int i = next;
            next = (next + 1) % Max;
            ref var p = ref pieces[i];
            if (!p.alive) active++;
            int s = (int)shape;
            p.tpl = (byte)(tplOfShape[s] + Random.Range(0, tplCount[s]));
            p.pos = pos;
            p.vel = vel;
            p.rot = Random.rotation;
            p.spin = Random.onUnitSphere * Random.Range(120f, 520f) * (shape == Shape.Leaf ? 1.6f : 1f);
            p.scale = size;
            p.age = 0f;
            p.life = life > 0f ? life : Random.Range(9f, 14f);
            p.drag = shape == Shape.Leaf ? 2.2f : shape == Shape.Glass ? 0.25f : 0.08f;
            p.gravity = shape == Shape.Leaf ? 0.22f : 1f;
            p.alive = true;
            p.resting = false;
            for (int k = 0; k < V; k++) cols[i * V + k] = col;
            colsDirty = true;
            Write(i);
        }

        // ---------------------------------------------------------------------
        // Simulation
        // ---------------------------------------------------------------------

        void Update()
        {
            if (active == 0 && !colsDirty) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            bool moved = false;
            for (int i = 0; i < Max; i++)
            {
                ref var p = ref pieces[i];
                if (!p.alive) continue;
                p.age += dt;
                if (p.age > p.life + 2f)
                {
                    p.alive = false;
                    active--;
                    Hide(i);
                    moved = true;
                    continue;
                }
                if (p.age > p.life)
                {
                    // Settled long enough: sink slowly into the ground and go.
                    p.pos.y -= dt * (0.35f + p.scale.y * 0.4f);
                    Write(i);
                    moved = true;
                    continue;
                }
                if (p.resting) continue;

                p.vel.y -= 9.81f * p.gravity * dt;
                p.vel *= 1f - Mathf.Min(1f, p.drag * dt);
                p.pos += p.vel * dt;
                if (p.spin.sqrMagnitude > 1f)
                    p.rot = Quaternion.AngleAxis(p.spin.magnitude * dt, p.spin.normalized) * p.rot;

                float ground = BattleGround.HeightAt(p.pos.x, p.pos.z) + p.scale.y * 0.35f;
                if (p.pos.y < ground)
                {
                    p.pos.y = ground;
                    if (p.vel.y < -2.5f && p.gravity > 0.5f)
                    {
                        // Bounce: a heavy, dull thud, losing most of its speed.
                        p.vel = new Vector3(p.vel.x * 0.45f, -p.vel.y * 0.22f, p.vel.z * 0.45f);
                        p.spin *= 0.5f;
                    }
                    else
                    {
                        // Lie flat on its broadest side and stop.
                        p.vel = Vector3.zero;
                        p.spin = Vector3.zero;
                        p.rot = Flatten(p.rot);
                        p.resting = true;
                    }
                }
                Write(i);
                moved = true;
            }
            if (moved || colsDirty)
            {
                mesh.vertices = verts;
                mesh.normals = norms;
                if (colsDirty) mesh.colors32 = cols;
                colsDirty = false;
            }
        }

        /// <summary>Turns a piece so its thinnest axis points up (how a slab comes to rest).</summary>
        static Quaternion Flatten(Quaternion r)
        {
            Vector3 up = r * Vector3.up;
            Vector3 fwd = r * Vector3.forward;
            Vector3 flatFwd = Vector3.ProjectOnPlane(fwd, Vector3.up);
            if (flatFwd.sqrMagnitude < 0.01f) flatFwd = Vector3.ProjectOnPlane(r * Vector3.right, Vector3.up);
            var flat = Quaternion.LookRotation(flatFwd.normalized, up.y >= 0f ? Vector3.up : Vector3.down);
            // Keep a little of the tilt so a heap doesn't look machined.
            return Quaternion.Slerp(flat, r, 0.15f);
        }

        void Write(int i)
        {
            ref var p = ref pieces[i];
            var tv = tplVerts[p.tpl];
            var tn = tplNorms[p.tpl];
            int b = i * V;
            for (int k = 0; k < V; k++)
            {
                Vector3 q = tv[k];
                q.x *= p.scale.x; q.y *= p.scale.y; q.z *= p.scale.z;
                verts[b + k] = p.pos + p.rot * q;
                norms[b + k] = p.rot * tn[k];
            }
        }

        void Hide(int i)
        {
            int b = i * V;
            for (int k = 0; k < V; k++) verts[b + k] = Vector3.zero;
        }

        // ---------------------------------------------------------------------
        // Templates: 8 corners of a unit box pushed about, 6 flat-shaded faces
        // ---------------------------------------------------------------------

        static void BuildTemplates()
        {
            if (tplVerts != null) return;
            var rnd = new System.Random(77);
            float J(float a) => ((float)rnd.NextDouble() * 2f - 1f) * a;
            var sets = new Vector3[8][];
            for (int t = 0; t < 8; t++)
            {
                var c = new Vector3[8];
                for (int k = 0; k < 8; k++)
                    c[k] = new Vector3((k & 1) == 0 ? -0.5f : 0.5f, (k & 2) == 0 ? -0.5f : 0.5f, (k & 4) == 0 ? -0.5f : 0.5f);
                if (t < 3)
                {
                    // Chunk: a broken lump, corners knocked in.
                    for (int k = 0; k < 8; k++) c[k] = Vector3.Scale(c[k], new Vector3(1f + J(0.35f), 1f + J(0.35f), 1f + J(0.35f)));
                }
                else if (t < 5)
                {
                    // Slab: a plate with a torn, skewed outline and a slight bend.
                    for (int k = 0; k < 8; k++) c[k] += new Vector3(J(0.18f), 0f, J(0.18f));
                    c[t == 3 ? 5 : 6].y += 0.6f;
                    c[t == 3 ? 7 : 4].y += 0.3f;
                }
                else if (t == 5)
                {
                    // Splinter: tapers to a point at one end.
                    for (int k = 4; k < 8; k++) c[k] = new Vector3(c[k].x * 0.2f, c[k].y * 0.3f, c[k].z);
                }
                else if (t == 6)
                {
                    // Leaf: a flat, pointed sliver.
                    for (int k = 0; k < 8; k++) if ((k & 4) != 0) c[k].x *= 0.15f;
                }
                else
                {
                    // Glass: a sharp triangular shard.
                    for (int k = 0; k < 8; k++) if ((k & 4) != 0) c[k].x = (k & 1) == 0 ? 0.5f : 0.45f;
                }
                sets[t] = c;
            }

            // Faces as corner indices, wound for Unity (clockwise seen from outside).
            int[][] faces =
            {
                new[] { 0, 2, 3, 1 }, // -z
                new[] { 5, 7, 6, 4 }, // +z
                new[] { 4, 6, 2, 0 }, // -x
                new[] { 1, 3, 7, 5 }, // +x
                new[] { 2, 6, 7, 3 }, // +y
                new[] { 4, 0, 1, 5 }, // -y
            };
            tplVerts = new Vector3[8][];
            tplNorms = new Vector3[8][];
            for (int t = 0; t < 8; t++)
            {
                var v = new Vector3[V];
                var n = new Vector3[V];
                for (int f = 0; f < 6; f++)
                {
                    var fc = faces[f];
                    Vector3 a = sets[t][fc[0]], b = sets[t][fc[1]], c = sets[t][fc[2]], d = sets[t][fc[3]];
                    Vector3 nn = Vector3.Cross(c - a, b - d);
                    nn = nn.sqrMagnitude > 1e-6f ? nn.normalized : Vector3.up;
                    // Keep the normal pointing out of the piece.
                    Vector3 mid = (a + b + c + d) * 0.25f;
                    if (Vector3.Dot(nn, mid) < 0f) nn = -nn;
                    for (int k = 0; k < 4; k++) { v[f * 4 + k] = sets[t][fc[k]]; n[f * 4 + k] = nn; }
                }
                tplVerts[t] = v;
                tplNorms[t] = n;
            }
        }
    }
}
