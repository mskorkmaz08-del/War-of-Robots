using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Giant robots don't walk around cars, trees and houses: they walk through them.
    /// Everything breaks with its own animation, in place inside its chunk mesh:
    /// cars buckle flat and burst their glass, trees and lamp posts crack at the base
    /// and topple away with a bounce, houses split, their walls fall outwards and the
    /// roof drops into the heap. Big buildings take explosion damage and when they give
    /// way they sink into their own dust cloud. Every piece thrown off (Rubble) takes
    /// the colours of the thing it came from.
    /// </summary>
    public class Crushables : MonoBehaviour
    {
        public static Crushables Instance { get; private set; }

        public class Chunk
        {
            public Mesh mesh;
            public Vector3[] verts, norms;
            public Color32[] cols;
            public int[] tris;
            public bool dirty, colsDirty;
        }

        class Anim
        {
            public int item;
            public float t, duration;
            public Vector3[] from;      // original vertex positions
            public Vector3[] fromN;     // and normals (rigid falls just turn them)
            public Vector3 away;        // direction it falls / spreads
            public float seed;
            public int stage;           // effects already fired
            public float lastStep;
        }

        const float GridCell = 16f;

        Dictionary<long, Chunk> chunks;
        List<PropWorld.Crush> items;
        bool[] done;
        float[] hp;
        Dictionary<int, Collider> colliders;
        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        readonly List<Anim> anims = new List<Anim>();
        float nextTick;

        public void Init(Dictionary<long, Chunk> chunkMeshes, List<PropWorld.Crush> list, Dictionary<int, Collider> buildingColliders)
        {
            Instance = this;
            chunks = chunkMeshes;
            items = list;
            colliders = buildingColliders ?? new Dictionary<int, Collider>();
            done = new bool[list.Count];
            hp = new float[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var it = list[i];
                long k = Cell(it.pivot.x, it.pivot.z);
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                l.Add(i);
                // Big buildings stand up to a few heavy shells; the bigger, the tougher.
                if (it.kind == PropWorld.Kind.Building)
                    hp[i] = 350f + it.radius * it.radius * it.height * 0.15f;
            }
            // Pieces swing out beyond the standing props: keep them from being culled.
            foreach (var c in chunks.Values)
            {
                var b = c.mesh.bounds;
                b.Expand(new Vector3(50f, 30f, 50f));
                c.mesh.bounds = b;
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        readonly Dictionary<Damageable, bool> robots = new Dictionary<Damageable, bool>();

        bool IsRobot(Damageable d)
        {
            if (!robots.TryGetValue(d, out bool r)) robots[d] = r = d.GetComponent<Locomotion>() != null;
            return r;
        }

        static long Cell(float x, float z) => (long)(Mathf.FloorToInt(x / GridCell) + 10000) * 100000 + (Mathf.FloorToInt(z / GridCell) + 10000);

        Camera tunedCam;

        void Update()
        {
            // The game camera can be swapped (hangar, respawn), so check it each frame.
            var cam = Camera.main;
            if (cam != tunedCam) { tunedCam = cam; WorldDetail.TuneCamera(cam); }
            if (items == null) return;
            if (Time.time >= nextTick)
            {
                nextTick = Time.time + 0.08f;
                foreach (var d in Damageable.All)
                {
                    if (d == null || d.IsDead || d.Body == null || d.Body.isKinematic) continue;
                    if (!IsRobot(d)) continue; // robots only, not depots
                    Vector3 p = d.transform.position;
                    float radius = Mathf.Max(3f, d.size * 1.9f);
                    // Walkers are tall: only what's under their feet, not what they jump over.
                    float ground = BattleGround.HeightAt(p.x, p.z);
                    if (p.y - ground > d.size * 9f) continue;
                    Visit(p, radius, Step, p, 0f);
                }
            }
            Animate(Time.deltaTime);
            Flush();
        }

        // What a visit does to each thing in reach (no delegates: nothing allocated per tick).
        const int Step = 0, SmallBlast = 1, BuildingHit = 2;

        void Visit(Vector3 p, float radius, int mode, Vector3 from, float damage)
        {
            float reach = radius + (mode == Step ? 8f : 30f);   // big buildings reach further from their centre
            int x0 = Mathf.FloorToInt((p.x - reach) / GridCell), x1 = Mathf.FloorToInt((p.x + reach) / GridCell);
            int z0 = Mathf.FloorToInt((p.z - reach) / GridCell), z1 = Mathf.FloorToInt((p.z + reach) / GridCell);
            for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                long k = (long)(x + 10000) * 100000 + (z + 10000);
                if (!grid.TryGetValue(k, out var list)) continue;
                foreach (int i in list)
                {
                    if (done[i]) continue;
                    var it = items[i];
                    float dx = it.pivot.x - p.x, dz = it.pivot.z - p.z;
                    float rr = radius + it.radius;
                    if (dx * dx + dz * dz >= rr * rr) continue;
                    bool building = it.kind == PropWorld.Kind.Building;
                    if (mode == Step || mode == SmallBlast)
                    {
                        if (!building) Break(i, from, mode == SmallBlast);
                    }
                    else if (building)
                    {
                        // Only blasts that actually reach the walls (not one far above the roof).
                        if (from.y - it.pivot.y > it.height + radius) continue;
                        hp[i] -= damage;
                        Chip(i, from, radius);
                        if (hp[i] <= 0f) Break(i, from, true);
                    }
                }
            }
        }

        /// <summary>
        /// Explosion: small things in about two thirds of the blast radius go over;
        /// big buildings in reach take the damage and chip, and fall when it's too much.
        /// </summary>
        public void Blast(Vector3 pos, float radius, float damage)
        {
            if (items == null) return;
            Visit(pos, radius * 0.65f, SmallBlast, pos, 0f);
            if (damage > 0f) Visit(pos, radius, BuildingHit, pos, damage);
        }

        void Flush()
        {
            foreach (var c in chunks.Values)
            {
                if (c.dirty)
                {
                    c.dirty = false;
                    c.mesh.vertices = c.verts;
                    c.mesh.normals = c.norms;
                }
                if (c.colsDirty)
                {
                    c.colsDirty = false;
                    c.mesh.colors32 = c.cols;
                }
            }
        }

        // ---------------------------------------------------------------------
        // Breaking
        // ---------------------------------------------------------------------

        void Break(int i, Vector3 from, bool blast)
        {
            if (done[i]) return;
            done[i] = true;
            var it = items[i];
            if (!chunks.TryGetValue(it.chunk, out var ch)) return;
            Vector3 away = new Vector3(it.pivot.x - from.x, 0f, it.pivot.z - from.z);
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;
            away.Normalize();

            var a = new Anim { item = i, away = away, seed = Random.value * 100f };
            a.from = new Vector3[it.end - it.start];
            System.Array.Copy(ch.verts, it.start, a.from, 0, a.from.Length);
            if (it.kind == PropWorld.Kind.Tree || it.kind == PropWorld.Kind.Pole)
            {
                a.fromN = new Vector3[a.from.Length];
                System.Array.Copy(ch.norms, it.start, a.fromN, 0, a.fromN.Length);
            }
            switch (it.kind)
            {
                case PropWorld.Kind.Car: a.duration = blast ? 0.35f : 0.22f; break;
                case PropWorld.Kind.Truck: a.duration = blast ? 0.45f : 0.3f; break;
                case PropWorld.Kind.Tree: a.duration = 0.9f + it.height * 0.05f; break;
                case PropWorld.Kind.Pole: a.duration = 0.7f + it.height * 0.04f; break;
                case PropWorld.Kind.House: a.duration = 2.2f; break;
                case PropWorld.Kind.Building: a.duration = 3.2f + it.height * 0.05f; break;
                default: a.duration = 0.5f; break;
            }
            // Blasts burn what they wreck.
            if (blast && (it.kind == PropWorld.Kind.Car || it.kind == PropWorld.Kind.Truck))
                Tint(ch, it, 0.3f, true);
            OnBreakStart(it, a, blast);
            anims.Add(a);

            // A broken building stops blocking shots and robots once it starts to fall.
            if (it.kind == PropWorld.Kind.Building && colliders.TryGetValue(i, out var col) && col != null)
                Destroy(col.gameObject, 0.6f);
        }

        void OnBreakStart(PropWorld.Crush it, Anim a, bool blast)
        {
            Vector3 p = it.pivot;
            var ch = chunks[it.chunk];
            switch (it.kind)
            {
                case PropWorld.Kind.Car:
                case PropWorld.Kind.Truck:
                {
                    int n = Palette(ch, it, 0f, 99f, Glassy);
                    if (n > 0) Rubble.Burst(Rubble.Shape.Glass, p + Vector3.up * 1.6f, new Vector3(1f, 0.4f, 1.6f), it.kind == PropWorld.Kind.Truck ? 16 : 10, pal, n, 0.12f, 0.3f, 6f, a.away * 2f, 0.8f);
                    n = Palette(ch, it, 0.6f, 99f, Painted);
                    if (n > 0) Rubble.Burst(Rubble.Shape.Slab, p + Vector3.up * 1.1f, new Vector3(1f, 0.3f, 1.8f), blast ? 6 : 3, pal, n, 0.4f, 0.9f, blast ? 12f : 5f, a.away * 2f);
                    Fx.Sparks(p + Vector3.up, Vector3.up, 14, 9f, 80f);
                    GameAudio.Play(Sfx.MetalHit, p, it.kind == PropWorld.Kind.Truck ? 0.8f : 0.6f, Random.Range(0.5f, 0.65f));
                    break;
                }
                case PropWorld.Kind.Tree:
                {
                    // The trunk cracks at the base: splinters of its own bark.
                    int n = Palette(ch, it, 0f, it.height * 0.3f, Any);
                    if (n > 0) Rubble.Burst(Rubble.Shape.Splinter, p + Vector3.up * 0.8f, new Vector3(0.4f, 0.4f, 0.4f), 5, pal, n, 0.5f, 1.4f, 5f, a.away * 3f);
                    GameAudio.Play(Sfx.Debris, p, 0.35f, Random.Range(1.3f, 1.5f));
                    break;
                }
                case PropWorld.Kind.Pole:
                {
                    Fx.Sparks(p + Vector3.up * 0.5f, a.away, 10, 8f, 70f);
                    GameAudio.Play(Sfx.MetalHit, p, 0.45f, Random.Range(0.8f, 1f));
                    break;
                }
                case PropWorld.Kind.House:
                {
                    // Glass bursts and plaster cracks off before the walls go.
                    int n = Palette(ch, it, 1f, it.height, Glassy);
                    if (n > 0) Rubble.Burst(Rubble.Shape.Glass, p + Vector3.up * it.height * 0.4f, new Vector3(4f, it.height * 0.3f, 4f), 14, pal, n, 0.15f, 0.45f, 7f, a.away * 3f, 0.4f);
                    n = Palette(ch, it, 0.5f, it.height, Walls);
                    if (n > 0) Rubble.Burst(Rubble.Shape.Chunk, p + Vector3.up * it.height * 0.5f, new Vector3(4f, it.height * 0.3f, 4f), 8, pal, n, 0.4f, 1.1f, blast ? 14f : 6f, a.away * 3f);
                    Fx.Dust(p + Vector3.up * 1.5f, 3f);
                    GameAudio.Play(Sfx.Debris, p, 0.9f, Random.Range(0.7f, 0.85f));
                    FollowCamera.Shake(0.06f, p, 60f);
                    break;
                }
                case PropWorld.Kind.Building:
                {
                    // The structure gives: a deep crack, glass rains, then it goes down.
                    int n = Palette(ch, it, 2f, it.height, Glassy);
                    if (n > 0) Rubble.Burst(Rubble.Shape.Glass, p + Vector3.up * it.height * 0.55f, new Vector3(it.radius, it.height * 0.4f, it.radius), 26, pal, n, 0.2f, 0.6f, 6f, a.away * 2f, 0.3f);
                    GameAudio.Play(Sfx.BigExplosion, p + Vector3.up * it.height * 0.3f, 0.7f, 0.55f);
                    GameAudio.Play(Sfx.Debris, p, 1f, 0.6f);
                    FollowCamera.Shake(0.25f, p, 160f);
                    break;
                }
            }
        }

        /// <summary>A shell hit a big building: chips fly off the wall and it's scorched.</summary>
        void Chip(int i, Vector3 pos, float radius)
        {
            var it = items[i];
            if (!chunks.TryGetValue(it.chunk, out var ch)) return;
            float h = Mathf.Clamp(pos.y - it.pivot.y, 0.5f, it.height);
            int n = Palette(ch, it, h - 4f, h + 4f, Walls);
            if (n == 0) n = Palette(ch, it, 0f, it.height, Any);
            Vector3 out_ = new Vector3(pos.x - it.pivot.x, 0f, pos.z - it.pivot.z).normalized;
            if (n > 0)
            {
                Rubble.Burst(Rubble.Shape.Chunk, pos, Vector3.one * 1.2f, 7, pal, n, 0.3f, 0.9f, 9f, out_ * 6f, 0.5f);
                Rubble.Burst(Rubble.Shape.Slab, pos, Vector3.one * 1.2f, 3, pal, n, 0.5f, 1.2f, 7f, out_ * 4f, 0.5f);
            }
            Fx.Dust(pos, 1.4f);
            // Blacken the wall around the hit.
            float r2 = radius * radius * 0.5f;
            var v = ch.verts;
            var c = ch.cols;
            for (int k = it.start; k < it.end; k++)
            {
                float d2 = (v[k] - pos).sqrMagnitude;
                if (d2 > r2) continue;
                float f = Mathf.Lerp(0.55f, 0.92f, d2 / r2);
                c[k] = new Color32((byte)(c[k].r * f), (byte)(c[k].g * f), (byte)(c[k].b * f), c[k].a);
            }
            ch.colsDirty = true;
        }

        // ---------------------------------------------------------------------
        // Animation
        // ---------------------------------------------------------------------

        void Animate(float dt)
        {
            for (int n = anims.Count - 1; n >= 0; n--)
            {
                var a = anims[n];
                a.t += dt;
                var it = items[a.item];
                if (!chunks.TryGetValue(it.chunk, out var ch)) { anims.RemoveAt(n); continue; }
                float s = Mathf.Clamp01(a.t / a.duration);
                switch (it.kind)
                {
                    case PropWorld.Kind.Car:
                    case PropWorld.Kind.Truck: Squash(ch, it, a, s); break;
                    case PropWorld.Kind.Tree:
                    case PropWorld.Kind.Pole: Topple(ch, it, a, s); break;
                    case PropWorld.Kind.House: Collapse(ch, it, a, s); break;
                    case PropWorld.Kind.Building: Implode(ch, it, a, s); break;
                }
                if (a.fromN == null) Normals(ch, it);
                ch.dirty = true;
                if (a.t >= a.duration + (it.kind == PropWorld.Kind.Tree || it.kind == PropWorld.Kind.Pole ? 0.5f : 0f))
                {
                    if (it.kind == PropWorld.Kind.House || it.kind == PropWorld.Kind.Building) Tint(ch, it, 0.62f, false);
                    anims.RemoveAt(n);
                }
            }
        }

        /// <summary>Cars and trucks: crushed flat in a quick buckle, tilted, bulging out.</summary>
        void Squash(Chunk ch, PropWorld.Crush it, Anim a, float s)
        {
            float e = 1f - (1f - s) * (1f - s);            // fast at first: a heavy foot
            float squash = Mathf.Lerp(1f, it.kind == PropWorld.Kind.Truck ? 0.4f : 0.32f, e);
            float tilt = Mathf.Sin(a.seed) * 0.12f * e;
            Vector3 pivot = it.pivot;
            var v = ch.verts;
            for (int k = it.start; k < it.end; k++)
            {
                Vector3 q = a.from[k - it.start] - pivot;
                float bulge = 1f + 0.07f * e * Mathf.Clamp01(q.y);
                q.y = q.y * squash + q.x * tilt;
                q.x *= bulge;
                q.z *= 1f + 0.03f * e;
                v[k] = pivot + q;
            }
            if (s >= 1f && a.stage == 0)
            {
                a.stage = 1;
                Fx.Dust(pivot, 0.8f);
            }
        }

        /// <summary>
        /// Trees and poles: snap at the base and fall away, slow at first and then fast,
        /// bounce once on the ground. The crown bursts into leaves where it lands.
        /// </summary>
        void Topple(Chunk ch, PropWorld.Crush it, Anim a, float s)
        {
            float fall = 84f + Mathf.Sin(a.seed) * 4f;
            float angle;
            if (s < 1f) angle = fall * s * s;
            else
            {
                // Bounce after hitting the ground: up a little, settle.
                float b = (a.t - a.duration) / 0.5f;
                angle = fall - 7f * Mathf.Sin(Mathf.Clamp01(b) * Mathf.PI) * (1f - Mathf.Clamp01(b) * 0.6f);
            }
            Vector3 axis = Vector3.Cross(Vector3.up, a.away);
            var rot = Quaternion.AngleAxis(angle, axis);
            Vector3 hinge = it.pivot + Vector3.up * 0.4f;
            var v = ch.verts;
            var nn = ch.norms;
            for (int k = it.start; k < it.end; k++)
            {
                Vector3 q = a.from[k - it.start] - hinge;
                if (q.y < 0f) { v[k] = a.from[k - it.start]; continue; }
                v[k] = hinge + rot * q;
                nn[k] = rot * a.fromN[k - it.start];
            }
            if (s >= 1f && a.stage == 0)
            {
                a.stage = 1;
                Vector3 tip = it.pivot + a.away * it.height * 0.65f;
                if (it.kind == PropWorld.Kind.Tree)
                {
                    int n = Palette(ch, it, it.height * 0.35f, 99f, Any);
                    if (n > 0) Rubble.Burst(Rubble.Shape.Leaf, tip + Vector3.up * 1.5f, new Vector3(2.5f, 1.2f, 2.5f), 16, pal, n, 0.25f, 0.5f, 5f, Vector3.up * 2f, 0.8f);
                    Fx.Dust(tip, 1.8f);
                    GameAudio.Play(Sfx.Debris, tip, 0.5f, Random.Range(0.9f, 1.1f));
                }
                else
                {
                    Fx.Sparks(tip, Vector3.up, 12, 7f, 120f);
                    Fx.Dust(tip, 0.9f);
                    GameAudio.Play(Sfx.MetalHit, tip, 0.5f, Random.Range(0.6f, 0.75f));
                }
                FollowCamera.Shake(0.03f, tip, 50f);
            }
        }

        /// <summary>
        /// Houses: each wall leans out and falls flat on its own beat, the roof drops
        /// into the middle, then everything settles into a heap of rubble.
        /// </summary>
        void Collapse(Chunk ch, PropWorld.Crush it, Anim a, float s)
        {
            Vector3 pivot = it.pivot;
            float h = Mathf.Max(3f, it.height);
            float wallTop = h * 0.62f;
            var yaw = Quaternion.Euler(0f, it.yaw, 0f);
            var inv = Quaternion.Inverse(yaw);
            float t = a.t;
            var v = ch.verts;
            for (int k = it.start; k < it.end; k++)
            {
                Vector3 o = a.from[k - it.start] - pivot;
                Vector3 l = inv * o;
                // Which side of the house is this vertex on.
                Vector3 n = Mathf.Abs(l.x) * 0.8f > Mathf.Abs(l.z) ? new Vector3(Mathf.Sign(l.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(l.z));
                int side = n.x > 0.5f ? 0 : n.x < -0.5f ? 1 : n.z > 0.5f ? 2 : 3;
                Vector3 wn = yaw * n;
                // The side facing away from the robot goes first; the others follow.
                float delay = 0.15f + 0.25f * (1f - Vector3.Dot(wn, a.away)) + Hash(side + a.seed) * 0.15f;
                float ls = Mathf.Clamp01((t - delay) / 1.1f);
                float e = ls * ls;
                Vector3 p;
                if (o.y > wallTop)
                {
                    // Roof: drops, tilts towards the fallen side, cracks into the heap.
                    float drop = Mathf.Min(o.y - 0.8f, 0.5f * 9.81f * Mathf.Max(0f, t - 0.35f) * Mathf.Max(0f, t - 0.35f) * 0.8f);
                    p = o + Vector3.down * drop + a.away * drop * 0.25f;
                }
                else
                {
                    // Wall: hinge at the foot of its own plane, swing outwards.
                    float off = Vector3.Dot(o, wn);
                    Vector3 hinge = wn * off;
                    Vector3 axis = Vector3.Cross(Vector3.up, wn);
                    p = hinge + Quaternion.AngleAxis(86f * e, axis) * (o - hinge);
                }
                // Finally everything breaks up and sinks into a low heap.
                float settle = Mathf.Clamp01((t - 1.3f) / 0.9f);
                if (settle > 0f)
                {
                    float jitter = Hash(k * 0.137f + a.seed);
                    Vector3 heap = new Vector3(o.x * 1.15f + a.away.x * o.y * 0.3f, Mathf.Max(0f, o.y) * 0.14f + jitter * 0.9f, o.z * 1.15f + a.away.z * o.y * 0.3f);
                    p = Vector3.Lerp(p, Vector3.Lerp(p, heap, 0.55f), settle * settle);
                    p.y = Mathf.Max(p.y, 0.05f);
                }
                v[k] = pivot + p;
            }
            // Dust as each wall hits the ground, then the settling cloud.
            if (a.stage == 0 && t > 0.9f)
            {
                a.stage = 1;
                Fx.Dust(pivot + a.away * 5f + Vector3.up, 4.5f);
                GameAudio.Play(Sfx.Debris, pivot, 0.9f, Random.Range(0.6f, 0.7f));
                int n = Palette(ch, it, 0f, h, Walls);
                if (n > 0) Rubble.Burst(Rubble.Shape.Chunk, pivot + a.away * 5f + Vector3.up, new Vector3(4f, 0.5f, 4f), 10, pal, n, 0.4f, 1.2f, 6f, a.away * 2f, 0.9f);
                FollowCamera.Shake(0.07f, pivot, 60f);
            }
            if (a.stage == 1 && t > 1.6f)
            {
                a.stage = 2;
                Fx.Dust(pivot + Vector3.up * 2f, 6.5f);
                int n = Palette(ch, it, wallTop, h + 5f, Any);
                if (n > 0) Rubble.Burst(Rubble.Shape.Slab, pivot + Vector3.up * 1.5f, new Vector3(4f, 1f, 4f), 8, pal, n, 0.6f, 1.4f, 5f, Vector3.zero, 0.9f);
            }
        }

        /// <summary>
        /// Big buildings: the lower floors give way and the whole block drops into
        /// itself, leaning a little, swallowed by a rolling cloud of its own dust.
        /// </summary>
        void Implode(Chunk ch, PropWorld.Crush it, Anim a, float s)
        {
            Vector3 pivot = it.pivot;
            float h = Mathf.Max(4f, it.height);
            float t = Mathf.Max(0f, a.t - 0.5f);           // a moment of creaking first
            float drop = 0.5f * 9.81f * 0.55f * t * t;      // giant scale: falls slow
            float lean = Mathf.Tan(Mathf.Min(9f, t * t * 3f) * Mathf.Deg2Rad);
            float rubbleTop = Mathf.Min(6f, h * 0.18f);
            var v = ch.verts;
            for (int k = it.start; k < it.end; k++)
            {
                Vector3 o = a.from[k - it.start] - pivot;
                float jitter = Hash(k * 0.173f + a.seed);
                // Where this point ends up in the heap.
                // (never above where the point started: the ground floor isn't lifted)
                float heapY = Mathf.Min(o.y, Mathf.Max(0f, o.y) / h * rubbleTop + jitter * 1.4f);
                Vector3 p = o;
                p.y -= drop;
                if (p.y < heapY)
                {
                    // Crushed: the slabs break up and tilt every which way in the heap.
                    float c = Mathf.Clamp01((heapY - p.y) / 4f);
                    p.y = heapY;
                    p.x += (Hash(k * 0.311f + a.seed) - 0.5f) * 1.4f * c;
                    p.z += (Hash(k * 0.517f + a.seed) - 0.5f) * 1.4f * c;
                }
                // The base bulges out as the floors pile into it, and the block leans
                // over. Both are linear in the height a point is at now, so flat walls
                // stay flat and windows stay on them.
                float yy = Mathf.Max(0f, p.y);
                float spread = 1f + 0.18f * Mathf.Clamp01(drop / h) * (1f - Mathf.Clamp01(yy / h) * 0.6f);
                p.x = p.x * spread + a.away.x * yy * lean;
                p.z = p.z * spread + a.away.z * yy * lean;
                v[k] = pivot + p;
            }
            // Dust curtain rolls out from the base in waves, debris rains off the edges.
            int wave = Mathf.FloorToInt(t / 0.55f);
            if (t > 0f && wave >= a.stage && a.stage < 6)
            {
                a.stage = wave + 1;
                float r = it.radius;
                for (int q = 0; q < 4; q++)
                {
                    float ang = (q * 90f + a.seed * 37f + a.stage * 23f) * Mathf.Deg2Rad;
                    Vector3 edge = pivot + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r * 0.9f;
                    Fx.Dust(edge + Vector3.up * 2f, 3.5f + r * 0.18f);
                }
                Fx.Dust(pivot + Vector3.up * Mathf.Max(2f, h - drop), 3f + r * 0.15f);
                int n = Palette(ch, it, 0f, h, Walls);
                if (n > 0)
                {
                    Vector3 top = pivot + Vector3.up * Mathf.Max(3f, h - drop);
                    Rubble.Burst(Rubble.Shape.Chunk, top, new Vector3(r, 2f, r), 10, pal, n, 0.6f, 1.8f, 8f, a.away * 3f, 0.5f);
                    Rubble.Burst(Rubble.Shape.Slab, top, new Vector3(r, 2f, r), 5, pal, n, 1f, 2.4f, 6f, a.away * 2f, 0.5f);
                }
                if (a.stage == 2) GameAudio.Play(Sfx.Debris, pivot, 1f, 0.5f);
                FollowCamera.Shake(0.12f, pivot, 180f);
            }
        }

        static float Hash(float x)
        {
            float f = Mathf.Sin(x * 12.9898f) * 43758.5453f;
            return f - Mathf.Floor(f);
        }

        /// <summary>Flat normals for the item's own triangles (its vertices aren't shared).</summary>
        static void Normals(Chunk ch, PropWorld.Crush it)
        {
            var v = ch.verts;
            var n = ch.norms;
            var t = ch.tris;
            for (int k = it.tStart; k + 2 < it.tEnd; k += 3)
            {
                int i0 = t[k], i1 = t[k + 1], i2 = t[k + 2];
                Vector3 nn = Vector3.Cross(v[i1] - v[i0], v[i2] - v[i0]);
                float m = nn.magnitude;
                if (m < 1e-6f) continue;
                nn /= m;
                n[i0] = nn; n[i1] = nn; n[i2] = nn;
            }
        }

        static void Tint(Chunk ch, PropWorld.Crush it, float k, bool burnt)
        {
            var c = ch.cols;
            // Dust-covered rubble goes pale grey-brown; burnt wrecks go black.
            Color32 dust = new Color32(118, 110, 100, 10);
            for (int i = it.start; i < it.end; i++)
            {
                var o = c[i];
                c[i] = burnt
                    ? new Color32((byte)(o.r * k), (byte)(o.g * k), (byte)(o.b * k), (byte)(o.a * 0.3f))
                    : new Color32((byte)Mathf.Lerp(dust.r, o.r * k, 0.5f), (byte)Mathf.Lerp(dust.g, o.g * k, 0.5f), (byte)Mathf.Lerp(dust.b, o.b * k, 0.5f), (byte)(o.a * 0.3f));
            }
            ch.colsDirty = true;
        }

        // ---------------------------------------------------------------------
        // Colours of the broken thing, for its debris
        // ---------------------------------------------------------------------

        static readonly Color32[] pal = new Color32[16];

        static bool Any(Color32 c, Vector3 n) => true;
        static bool Glassy(Color32 c, Vector3 n) => c.a >= 200 && c.r + c.g + c.b < 200;
        static bool Painted(Color32 c, Vector3 n) => c.a >= 120 && !Glassy(c, n);
        static bool Walls(Color32 c, Vector3 n) => Mathf.Abs(n.y) < 0.5f && !Glassy(c, n);

        /// <summary>
        /// Picks up to 16 colours off the object's own surface between two heights above
        /// its base, weighted by area, so debris has the same mix as the thing itself.
        /// </summary>
        int Palette(Chunk ch, PropWorld.Crush it, float yMin, float yMax, System.Func<Color32, Vector3, bool> keep)
        {
            var v = ch.verts;
            var t = ch.tris;
            int tris = (it.tEnd - it.tStart) / 3;
            if (tris <= 0) return 0;
            int count = 0;
            for (int tries = 0; tries < 64 && count < pal.Length; tries++)
            {
                int k = it.tStart + Random.Range(0, tris) * 3;
                int i0 = t[k], i1 = t[k + 1], i2 = t[k + 2];
                if (i0 >= ch.cols.Length || i0 < it.start || i0 >= it.end) continue;
                float y = (v[i0].y + v[i1].y + v[i2].y) / 3f - it.pivot.y;
                if (y < yMin || y > yMax) continue;
                Vector3 nn = Vector3.Cross(v[i1] - v[i0], v[i2] - v[i0]);
                float area = nn.magnitude;
                // Bigger faces are picked more often (they make up more of the object).
                if (area < 1e-4f || Random.value > Mathf.Clamp01(area * 0.5f + 0.15f)) continue;
                var c = ch.cols[i0];
                if (!keep(c, nn / area)) continue;
                pal[count++] = c;
            }
            return count;
        }
    }
}
