using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RobotArena
{
    /// <summary>
    /// The ground of every map: a height-field mesh with mounds, dunes or drifts in the
    /// open, flat where buildings, cover, spawns and objectives stand, painted with
    /// photo-scanned layers (RobotArenaTerrain.shader). Vertex colours pick the layers:
    /// R = patches (rubble, gravel, ice), G = rock on slopes, B = contact darkening.
    /// </summary>
    public static class BattleGround
    {
        const float Half = Frontier.Half;
        const float Fine = 2.5f;      // metres between vertices inside the battlefield
        const float Coarse = 16f;     // ... and in the ring outside it
        const float Ring = 160f;      // how far the mesh reaches past the battlefield edge
        const float GridCell = 4f;    // clearance grid
        const float GridHalf = Half + 40f;

        static readonly Dictionary<int, Material> farCache = new Dictionary<int, Material>();

        static int Biome(MapId map)
        {
            switch (map)
            {
                case MapId.Dockyard: return 1;
                case MapId.Canyon: return 2;
                case MapId.FrozenPass: return 3;
                case MapId.RuinedCity: return 4;
                default: return 0;
            }
        }

        /// <summary>The map's ground material, or null if the build has none. far = coarser tiling for mountains.</summary>
        public static Material Mat(MapId map, bool far = false)
        {
            var a = GameAssets.Get();
            int b = Biome(map);
            if (a == null || a.terrainMats == null || a.terrainMats.Length <= b || a.terrainMats[b] == null) return null;
            var m = a.terrainMats[b];
            if (!far) return m;
            if (farCache.TryGetValue(b, out var f) && f != null) return f;
            f = new Material(m) { name = m.name + "_Far" };
            foreach (var p in new[] { "_BaseTiling", "_PatchTiling", "_RockTiling" })
                if (f.HasProperty(p)) f.SetFloat(p, f.GetFloat(p) * 3f);
            if (f.HasProperty("_Macro")) f.SetFloat("_Macro", 0.8f);
            farCache[b] = f;
            return f;
        }

        public static bool Build(Transform root, MapId map, MapLayout layout)
        {
            var mat = Mat(map);
            if (mat == null) return false;

            Physics.SyncTransforms();
            float[,] clear = ClearanceField();

            // Vertex positions: fine inside the battlefield, coarse outside.
            var xs = new List<float>();
            for (float v = -Half - Ring; v < -Half - 0.01f; v += Coarse) xs.Add(v);
            for (float v = -Half; v < Half - 0.01f; v += Fine) xs.Add(v);
            for (float v = Half; v <= Half + Ring + 0.01f; v += Coarse) xs.Add(v);
            int n = xs.Count;

            float amp, scale; bool dunes;
            switch (map)
            {
                case MapId.Canyon: amp = 7f; scale = 0.016f; dunes = true; break;
                case MapId.FrozenPass: amp = 5f; scale = 0.014f; dunes = true; break;
                case MapId.Dockyard: amp = 1.2f; scale = 0.02f; dunes = false; break;
                case MapId.RuinedCity: amp = 1.6f; scale = 0.02f; dunes = false; break;
                default: amp = 2.6f; scale = 0.015f; dunes = false; break;
            }
            float s = (int)map * 31.7f + 3.3f;

            var verts = new Vector3[n * n];
            var nearBlock = new float[n * n];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = xs[i], z = xs[j];
                float dBlock = Sample(clear, x, z);
                nearBlock[j * n + i] = dBlock;
                float flat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 20f, dBlock));
                flat = Mathf.Min(flat, LayoutClear(layout, x, z));
                // Fade out towards the edge of the mesh so it meets the plain ground.
                float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Half + 10f, Half + 40f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z))));

                float n1 = Mathf.PerlinNoise(x * scale + s, z * scale + s * 0.7f);
                float relief = dunes
                    ? Mathf.Pow(1f - Mathf.Abs(n1 * 2f - 1f), 2f) * Mathf.PerlinNoise(x * 0.006f + s, z * 0.006f) * 1.6f
                    : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.85f, n1));
                relief += (Mathf.PerlinNoise(x * 0.07f + s, z * 0.07f + 5f) - 0.3f) * 0.12f;
                float h = Mathf.Max(0f, amp * relief) * flat * edge;
                verts[j * n + i] = new Vector3(x, h, z);
            }

            var tris = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < n - 1; i++)
            {
                int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }

            var mesh = new Mesh { name = "BattleGround" };
            if (verts.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var normals = mesh.normals;
            var colors = new Color[verts.Length];
            for (int k = 0; k < verts.Length; k++)
            {
                Vector3 p = verts[k];
                float near = 1f - Mathf.InverseLerp(0f, 10f, nearBlock[k]);
                float patch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.7f, Mathf.PerlinNoise(p.x * 0.035f + s, p.z * 0.035f - s)));
                patch = Mathf.Max(patch, near * 0.75f);
                float rock = Mathf.InverseLerp(0.93f, 0.78f, normals[k].y);
                float dark = Mathf.Lerp(0.72f, 1f, Mathf.InverseLerp(0f, 6f, nearBlock[k]));
                colors[k] = new Color(patch, rock, dark, 1f);
            }
            mesh.colors = colors;

            var go = new GameObject("BattleGround");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return true;
        }

        /// <summary>Vertex colours for the mountains: rock on the steep faces, snow/sand/dirt where it can settle.</summary>
        public static void PaintSlopes(Mesh mesh, MapId map)
        {
            var v = mesh.vertices;
            var nrm = mesh.normals;
            var c = new Color[v.Length];
            bool snowy = map == MapId.FrozenPass;
            for (int k = 0; k < v.Length; k++)
            {
                float steep = snowy ? Mathf.InverseLerp(0.8f, 0.6f, nrm[k].y) : Mathf.InverseLerp(0.92f, 0.75f, nrm[k].y);
                // Higher up, less soil holds on (except snow, which caps the peaks).
                float high = snowy ? 0f : Mathf.InverseLerp(20f, 90f, v[k].y) * 0.6f;
                float patch = Mathf.PerlinNoise(v[k].x * 0.02f, v[k].z * 0.02f);
                c[k] = new Color(patch, Mathf.Clamp01(steep + high), 1f, 1f);
            }
            mesh.colors = c;
        }

        // ---------------------------------------------------------------------
        // Clearance: distance from every point to the nearest thing standing on the ground
        // ---------------------------------------------------------------------

        static float[,] ClearanceField()
        {
            int g = Mathf.CeilToInt(GridHalf * 2f / GridCell) + 1;
            var dist = new float[g, g];
            var hits = new Collider[16];
            const float Big = 9999f;
            for (int j = 0; j < g; j++)
            for (int i = 0; i < g; i++)
            {
                var c = new Vector3(-GridHalf + i * GridCell, 1.5f, -GridHalf + j * GridCell);
                int count = Physics.OverlapSphereNonAlloc(c, GridCell * 0.75f, hits, ~(1 << 2), QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int h = 0; h < count && !blocked; h++)
                {
                    var col = hits[h];
                    if (col == null) continue;
                    var b = col.bounds;
                    // The plain ground, the invisible edge and the mountains don't count.
                    if (b.size.x > 300f || b.size.z > 300f || col.name == "Mountains" || col.name == "BattleGround") continue;
                    if (b.max.y < 0.4f) continue;
                    blocked = true;
                }
                dist[i, j] = blocked ? 0f : Big;
            }
            // Two-pass chamfer distance transform (metres).
            float d1 = GridCell, d2 = GridCell * 1.414f;
            for (int j = 0; j < g; j++)
            for (int i = 0; i < g; i++)
            {
                float v = dist[i, j];
                if (i > 0) v = Mathf.Min(v, dist[i - 1, j] + d1);
                if (j > 0) v = Mathf.Min(v, dist[i, j - 1] + d1);
                if (i > 0 && j > 0) v = Mathf.Min(v, dist[i - 1, j - 1] + d2);
                if (i < g - 1 && j > 0) v = Mathf.Min(v, dist[i + 1, j - 1] + d2);
                dist[i, j] = v;
            }
            for (int j = g - 1; j >= 0; j--)
            for (int i = g - 1; i >= 0; i--)
            {
                float v = dist[i, j];
                if (i < g - 1) v = Mathf.Min(v, dist[i + 1, j] + d1);
                if (j < g - 1) v = Mathf.Min(v, dist[i, j + 1] + d1);
                if (i < g - 1 && j < g - 1) v = Mathf.Min(v, dist[i + 1, j + 1] + d2);
                if (i > 0 && j < g - 1) v = Mathf.Min(v, dist[i - 1, j + 1] + d2);
                dist[i, j] = v;
            }
            return dist;
        }

        static float Sample(float[,] f, float x, float z)
        {
            int g = f.GetLength(0);
            float fx = (x + GridHalf) / GridCell, fz = (z + GridHalf) / GridCell;
            if (fx < 0f || fz < 0f || fx > g - 1 || fz > g - 1) return 9999f;
            int i = Mathf.Min((int)fx, g - 2), j = Mathf.Min((int)fz, g - 2);
            float u = fx - i, w = fz - j;
            return Mathf.Lerp(Mathf.Lerp(f[i, j], f[i + 1, j], u), Mathf.Lerp(f[i, j + 1], f[i + 1, j + 1], u), w);
        }

        /// <summary>0 at spawns and objectives (kept flat), rising to 1 well away from them.</summary>
        static float LayoutClear(MapLayout l, float x, float z)
        {
            if (l == null) return 1f;
            float m = 1f;
            void Point(Vector3 p, float r)
            {
                float d = Mathf.Sqrt((p.x - x) * (p.x - x) + (p.z - z) * (p.z - z));
                m = Mathf.Min(m, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r, r + 20f, d)));
            }
            if (l.allySpawns != null) foreach (var p in l.allySpawns) Point(p, 12f);
            if (l.enemySpawns != null) foreach (var p in l.enemySpawns) Point(p, 12f);
            if (l.zones != null) foreach (var p in l.zones) Point(p, l.zoneRadius + 4f);
            if (l.depots != null) foreach (var p in l.depots) Point(p, 10f);
            if (l.reactor != Vector3.zero) Point(l.reactor, 14f);
            return m;
        }
    }
}
