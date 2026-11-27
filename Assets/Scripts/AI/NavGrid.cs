using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Pure grid path finder (plain C#, no Unity calls, so it can be tested outside Unity): an 8-way A* over a square grid where each
    /// cell stores its clearance, the distance in cells to the nearest solid cell
    /// (0 = solid). A robot that needs clearance c only walks cells with clearance >= c,
    /// so big machines keep away from walls and never try to squeeze down an alley.
    /// Cells near walls cost a little more, so paths run down the middle of streets.
    /// </summary>
    public class GridPaths
    {
        public readonly int n;
        public readonly byte[] clear;

        float[] g;
        int[] parent;
        int[] stamp;
        int curStamp;
        int[] heap;
        int heapCount;
        float[] f;
        bool[] closed;

        public GridPaths(int n)
        {
            this.n = n;
            clear = new byte[n * n];
            g = new float[n * n];
            f = new float[n * n];
            parent = new int[n * n];
            stamp = new int[n * n];
            closed = new bool[n * n];
            heap = new int[n * n];
        }

        public bool In(int x, int z) => x >= 0 && z >= 0 && x < n && z < n;
        public int Clear(int x, int z) => In(x, z) ? clear[z * n + x] : 0;

        /// <summary>Fills clearance from solid flags with a two-pass chamfer distance (in cells, capped at 250).</summary>
        public void ComputeClearance(bool[] solid)
        {
            const int Big = 250;
            var d = new int[n * n];
            for (int i = 0; i < d.Length; i++) d[i] = solid[i] ? 0 : Big;
            // Treat outside the grid as solid so robots keep off the map edge.
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    if (d[i] == 0) continue;
                    int edge = System.Math.Min(System.Math.Min(x + 1, z + 1), System.Math.Min(n - x, n - z));
                    if (edge < d[i]) d[i] = edge;
                }
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    int v = d[i];
                    if (v == 0) continue;
                    if (x > 0) v = System.Math.Min(v, d[i - 1] + 1);
                    if (z > 0) v = System.Math.Min(v, d[i - n] + 1);
                    if (x > 0 && z > 0) v = System.Math.Min(v, d[i - n - 1] + 1);
                    if (x < n - 1 && z > 0) v = System.Math.Min(v, d[i - n + 1] + 1);
                    d[i] = v;
                }
            for (int z = n - 1; z >= 0; z--)
                for (int x = n - 1; x >= 0; x--)
                {
                    int i = z * n + x;
                    int v = d[i];
                    if (v == 0) continue;
                    if (x < n - 1) v = System.Math.Min(v, d[i + 1] + 1);
                    if (z < n - 1) v = System.Math.Min(v, d[i + n] + 1);
                    if (x < n - 1 && z < n - 1) v = System.Math.Min(v, d[i + n + 1] + 1);
                    if (x > 0 && z < n - 1) v = System.Math.Min(v, d[i + n - 1] + 1);
                    d[i] = v;
                }
            for (int i = 0; i < d.Length; i++) clear[i] = (byte)System.Math.Min(d[i], Big);
        }

        /// <summary>The nearest cell with at least this clearance, searching outward in rings.</summary>
        public bool Nearest(int x, int z, int need, int maxRing, out int ox, out int oz)
        {
            ox = x; oz = z;
            if (Clear(x, z) >= need) return true;
            for (int r = 1; r <= maxRing; r++)
            {
                int best = -1;
                float bestD = float.MaxValue;
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (System.Math.Abs(dx) != r && System.Math.Abs(dz) != r) continue;
                        int cx = x + dx, cz = z + dz;
                        if (Clear(cx, cz) < need) continue;
                        float dd = dx * dx + dz * dz;
                        if (dd < bestD) { bestD = dd; best = cz * n + cx; }
                    }
                if (best >= 0)
                {
                    ox = best % n;
                    oz = best / n;
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when every cell on the straight line between two cells has the clearance.</summary>
        public bool LineClear(int x0, int z0, int x1, int z1, int need)
        {
            int dx = System.Math.Abs(x1 - x0), dz = System.Math.Abs(z1 - z0);
            int sx = x0 < x1 ? 1 : -1, sz = z0 < z1 ? 1 : -1;
            int err = dx - dz;
            int x = x0, z = z0;
            int guard = dx + dz + 2;
            while (guard-- > 0)
            {
                if (Clear(x, z) < need) return false;
                if (x == x1 && z == z1) return true;
                int e2 = 2 * err;
                // Step both ways on a diagonal move so the line can't slip between two corners.
                if (e2 > -dz && e2 < dx)
                {
                    if (Clear(x + sx, z) < need || Clear(x, z + sz) < need) return false;
                }
                if (e2 > -dz) { err -= dz; x += sx; }
                if (e2 < dx) { err += dx; z += sz; }
            }
            return true;
        }

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly float[] Cost = { 1f, 1f, 1f, 1f, 1.4142f, 1.4142f, 1.4142f, 1.4142f };

        /// <summary>
        /// A* from start to goal over cells with clearance >= need. Fills cells (start first)
        /// with packed indices (z * n + x). When the goal can't be reached within the
        /// expansion budget, returns the path to the explored cell closest to the goal and false.
        /// </summary>
        public bool Find(int sx, int sz, int gx, int gz, int need, List<int> cells, int budget = 9000)
        {
            cells.Clear();
            if (!In(sx, sz) || !In(gx, gz)) return false;
            curStamp++;
            if (curStamp == int.MaxValue) { System.Array.Clear(stamp, 0, stamp.Length); curStamp = 1; }
            heapCount = 0;
            int start = sz * n + sx, goal = gz * n + gx;
            Touch(start);
            g[start] = 0f;
            f[start] = H(sx, sz, gx, gz);
            parent[start] = -1;
            Push(start);
            int best = start;
            float bestH = f[start];
            bool found = false;
            while (heapCount > 0 && budget-- > 0)
            {
                int cur = Pop();
                if (closed[cur]) continue;
                closed[cur] = true;
                if (cur == goal) { found = true; best = cur; break; }
                int cx = cur % n, cz = cur / n;
                float h = H(cx, cz, gx, gz);
                if (h < bestH) { bestH = h; best = cur; }
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + DX[k], nz = cz + DZ[k];
                    if (!In(nx, nz)) continue;
                    int ni = nz * n + nx;
                    int c = clear[ni];
                    if (c < need) continue;
                    // No cutting corners past a solid cell.
                    if (k >= 4 && (clear[cz * n + nx] < need || clear[nz * n + cx] < need)) continue;
                    // Hug the middle of open ground: cells just wide enough cost more.
                    float step = Cost[k] * (c <= need ? 1.6f : c == need + 1 ? 1.25f : 1f);
                    Touch(ni);
                    if (closed[ni]) continue;
                    float ng = g[cur] + step;
                    if (ng >= g[ni]) continue;
                    g[ni] = ng;
                    f[ni] = ng + H(nx, nz, gx, gz);
                    parent[ni] = cur;
                    Push(ni);
                }
            }
            for (int c = best; c >= 0; c = parent[c]) cells.Add(c);
            cells.Reverse();
            return found;
        }

        void Touch(int i)
        {
            if (stamp[i] == curStamp) return;
            stamp[i] = curStamp;
            g[i] = float.MaxValue;
            closed[i] = false;
            parent[i] = -1;
        }

        static float H(int x, int z, int gx, int gz)
        {
            int dx = System.Math.Abs(x - gx), dz = System.Math.Abs(z - gz);
            return (dx + dz) + (1.4142f - 2f) * System.Math.Min(dx, dz);
        }

        void Push(int i)
        {
            int k = heapCount++;
            heap[k] = i;
            while (k > 0)
            {
                int p = (k - 1) >> 1;
                if (f[heap[p]] <= f[heap[k]]) break;
                (heap[p], heap[k]) = (heap[k], heap[p]);
                k = p;
            }
        }

        int Pop()
        {
            int top = heap[0];
            heap[0] = heap[--heapCount];
            int k = 0;
            while (true)
            {
                int l = k * 2 + 1, r = l + 1, m = k;
                if (l < heapCount && f[heap[l]] < f[heap[m]]) m = l;
                if (r < heapCount && f[heap[r]] < f[heap[m]]) m = r;
                if (m == k) break;
                (heap[m], heap[k]) = (heap[k], heap[m]);
                k = m;
            }
            return top;
        }

        /// <summary>Drops waypoints that a straight walk can skip (string pulling).</summary>
        public void Smooth(List<int> cells, int need)
        {
            if (cells.Count < 3) return;
            var outList = new List<int> { cells[0] };
            int anchor = 0;
            while (anchor < cells.Count - 1)
            {
                int far = anchor + 1;
                int ax = cells[anchor] % n, az = cells[anchor] / n;
                // Look ahead a bounded distance for the furthest cell still in a straight line of sight.
                int limit = System.Math.Min(cells.Count - 1, anchor + 40);
                for (int j = limit; j > anchor + 1; j--)
                {
                    if (LineClear(ax, az, cells[j] % n, cells[j] / n, need)) { far = j; break; }
                }
                outList.Add(cells[far]);
                anchor = far;
            }
            cells.Clear();
            cells.AddRange(outList);
        }
    }

    /// <summary>
    /// The battlefield as the AI sees it: an 8 m grid marking where buildings, walls,
    /// rocks and the map edge stand. Built from the physics colliders the first time a
    /// robot asks for a route (the world is finished by then), and dropped when a new map
    /// is built. Props robots crush (cars, trees, houses) have no colliders, so they
    /// don't block: giant robots walk through them.
    /// </summary>
    public static class NavGrid
    {
        public const float Cell = 8f;
        static GridPaths paths;
        static float min;
        static int framePaths;
        static int frame = -1;

        public static bool Ready => paths != null;

        /// <summary>Forget the current map (called when a new map is built).</summary>
        public static void Reset() => paths = null;

        static readonly Collider[] probe = new Collider[24];

        static void Build()
        {
            float half = Frontier.Half;
            int n = Mathf.CeilToInt(half * 2f / Cell);
            min = -n * Cell * 0.5f;
            paths = new GridPaths(n);
            var solid = new bool[n * n];
            Physics.SyncTransforms();
            Vector3 ext = new Vector3(Cell * 0.45f, 5f, Cell * 0.45f);
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    Vector3 c = CellCentre(x, z);
                    float h = BattleGround.HeightAt(c.x, c.z);
                    c.y = h + 3.2f + ext.y;
                    int hits = Physics.OverlapBoxNonAlloc(c, ext, probe, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < hits; i++)
                        if (Blocks(probe[i])) { solid[z * n + x] = true; break; }
                }
            paths.ComputeClearance(solid);
        }

        static bool Blocks(Collider c)
        {
            if (c == null) return false;
            var rb = c.attachedRigidbody;
            // Robots and loose debris move: steer around them locally instead.
            if (rb != null && !rb.isKinematic) return false;
            // The walkable ground itself (rolling hills) never blocks.
            if (c.gameObject.name == "BattleGround") return false;
            // Low kerbs and slabs a giant just steps over.
            if (c.bounds.max.y < BattleGround.HeightAt(c.bounds.center.x, c.bounds.center.z) + 3f) return false;
            return true;
        }

        static void Ensure()
        {
            if (paths == null) Build();
        }

        public static Vector3 CellCentre(int x, int z) => new Vector3(min + (x + 0.5f) * Cell, 0f, min + (z + 0.5f) * Cell);

        static void ToCell(Vector3 p, out int x, out int z)
        {
            x = Mathf.FloorToInt((p.x - min) / Cell);
            z = Mathf.FloorToInt((p.z - min) / Cell);
        }

        /// <summary>Cells of clearance a robot of this footprint radius (metres) needs.</summary>
        public static int NeedFor(float radius) => Mathf.Clamp(Mathf.CeilToInt(radius / Cell), 1, 3);

        /// <summary>Whether a robot with this clearance can stand at p.</summary>
        public static bool Walkable(Vector3 p, int need)
        {
            Ensure();
            ToCell(p, out int x, out int z);
            return paths.Clear(x, z) >= need;
        }

        /// <summary>Straight walk from a to b is free of buildings for this clearance.</summary>
        public static bool StraightClear(Vector3 a, Vector3 b, int need)
        {
            Ensure();
            ToCell(a, out int ax, out int az);
            ToCell(b, out int bx, out int bz);
            int s = Mathf.Max(1, need - 1);
            return paths.LineClear(ax, az, bx, bz, s);
        }

        /// <summary>A nearby open spot for this clearance (p itself when it is open).</summary>
        public static Vector3 OpenSpot(Vector3 p, int need)
        {
            Ensure();
            ToCell(p, out int x, out int z);
            if (paths.Nearest(x, z, need, 12, out int ox, out int oz) && (ox != x || oz != z))
                return CellCentre(ox, oz);
            return p;
        }

        /// <summary>Paths per frame are limited so a whole team repathing at once can't cause a hitch.</summary>
        public static bool CanPathThisFrame
        {
            get
            {
                if (Time.frameCount != frame) { frame = Time.frameCount; framePaths = 0; }
                return framePaths < 2;
            }
        }

        static readonly List<int> scratch = new List<int>();

        /// <summary>
        /// A route of world waypoints from 'from' to 'to' that a robot needing this clearance
        /// can walk. Falls back to less clearance when the big route is closed. Returns false
        /// (with the best partial route) when the goal can't be reached.
        /// </summary>
        public static bool Route(Vector3 from, Vector3 to, int need, List<Vector3> route)
        {
            Ensure();
            if (Time.frameCount != frame) { frame = Time.frameCount; framePaths = 0; }
            framePaths++;
            route.Clear();
            ToCell(from, out int sx, out int sz);
            ToCell(to, out int gx, out int gz);
            bool ok = false;
            for (int c = need; c >= 1; c--)
            {
                if (!paths.Nearest(sx, sz, c, 6, out int ax, out int az)) continue;
                if (!paths.Nearest(gx, gz, c, 10, out int bx, out int bz)) continue;
                ok = paths.Find(ax, az, bx, bz, c, scratch);
                if (scratch.Count == 0) continue;
                paths.Smooth(scratch, c);
                if (ok || c == 1) break;
            }
            for (int i = 1; i < scratch.Count; i++)
            {
                int cell = scratch[i];
                route.Add(CellCentre(cell % paths.n, cell / paths.n));
            }
            // End exactly on the goal when it is open ground.
            if (ok && route.Count > 0 && Walkable(to, 1)) route[route.Count - 1] = new Vector3(to.x, 0f, to.z);
            if (route.Count == 0) route.Add(new Vector3(to.x, 0f, to.z));
            return ok;
        }
    }
}
