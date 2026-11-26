using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public enum Shape { Box, Cylinder, Sphere, Wedge, Hex }

    /// <summary>
    /// Procedural unit meshes (all fit in a 1x1x1 box centred on the origin, the
    /// cylinder and hex along Y). Generated in code so they can be merged at runtime.
    /// </summary>
    public static class Shapes
    {
        static readonly Dictionary<Shape, Mesh> Cache = new Dictionary<Shape, Mesh>();

        public static Mesh Get(Shape shape)
        {
            if (Cache.TryGetValue(shape, out var m) && m != null) return m;
            switch (shape)
            {
                case Shape.Cylinder: m = Prism(20); break;
                case Shape.Hex: m = Prism(6); break;
                case Shape.Sphere: m = Sphere(16, 10); break;
                case Shape.Wedge: m = Wedge(); break;
                default: m = Box(); break;
            }
            m.name = shape.ToString();
            Cache[shape] = m;
            return m;
        }

        static Mesh Box()
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left };
            foreach (var d in dirs) AddQuadFace(v, n, uv, t, d);
            return Build(v, n, uv, t);
        }

        static void AddQuadFace(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 normal)
        {
            Vector3 up = Mathf.Abs(normal.y) > 0.5f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(up, normal);
            Vector3 c = normal * 0.5f;
            int i = v.Count;
            v.Add(c - right * 0.5f - up * 0.5f);
            v.Add(c + right * 0.5f - up * 0.5f);
            v.Add(c + right * 0.5f + up * 0.5f);
            v.Add(c - right * 0.5f + up * 0.5f);
            for (int k = 0; k < 4; k++) n.Add(normal);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        static Mesh Prism(int sides)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();

            // Sides (hard edges for hex, smooth for cylinder).
            bool smooth = sides > 8;
            for (int s = 0; s < sides; s++)
            {
                float a0 = s / (float)sides * Mathf.PI * 2f;
                float a1 = (s + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.5f;
                Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.5f;
                Vector3 n0 = smooth ? p0.normalized : ((p0 + p1) * 0.5f).normalized;
                Vector3 n1 = smooth ? p1.normalized : n0;
                int i = v.Count;
                v.Add(p0 + Vector3.down * 0.5f); v.Add(p1 + Vector3.down * 0.5f);
                v.Add(p1 + Vector3.up * 0.5f); v.Add(p0 + Vector3.up * 0.5f);
                n.Add(n0); n.Add(n1); n.Add(n1); n.Add(n0);
                float u0 = s / (float)sides, u1 = (s + 1) / (float)sides;
                uv.Add(new Vector2(u0, 0)); uv.Add(new Vector2(u1, 0)); uv.Add(new Vector2(u1, 1)); uv.Add(new Vector2(u0, 1));
                t.Add(i); t.Add(i + 2); t.Add(i + 1);
                t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }

            // Caps.
            for (int cap = 0; cap < 2; cap++)
            {
                float y = cap == 0 ? 0.5f : -0.5f;
                Vector3 normal = cap == 0 ? Vector3.up : Vector3.down;
                int center = v.Count;
                v.Add(new Vector3(0f, y, 0f)); n.Add(normal); uv.Add(new Vector2(0.5f, 0.5f));
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(a) * 0.5f, y, Mathf.Sin(a) * 0.5f));
                    n.Add(normal);
                    uv.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
                }
                for (int s = 0; s < sides; s++)
                {
                    if (cap == 0) { t.Add(center); t.Add(center + s + 2); t.Add(center + s + 1); }
                    else { t.Add(center); t.Add(center + s + 1); t.Add(center + s + 2); }
                }
            }
            return Build(v, n, uv, t);
        }

        static Mesh Sphere(int lon, int lat)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float va = y / (float)lat * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float ha = x / (float)lon * Mathf.PI * 2f;
                    Vector3 p = new Vector3(Mathf.Sin(va) * Mathf.Cos(ha), Mathf.Cos(va), Mathf.Sin(va) * Mathf.Sin(ha));
                    v.Add(p * 0.5f);
                    n.Add(p);
                    uv.Add(new Vector2(x / (float)lon, 1f - y / (float)lat));
                }
            }
            for (int y = 0; y < lat; y++)
            for (int x = 0; x < lon; x++)
            {
                int a = y * (lon + 1) + x;
                int b = a + lon + 1;
                t.Add(a); t.Add(a + 1); t.Add(b);
                t.Add(a + 1); t.Add(b + 1); t.Add(b);
            }
            return Build(v, n, uv, t);
        }

        /// <summary>Ramp shape: full bottom, slope rising from front (z+) to back (z-).</summary>
        static Mesh Wedge()
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();

            Vector3 bfl = new Vector3(-0.5f, -0.5f, 0.5f), bfr = new Vector3(0.5f, -0.5f, 0.5f);
            Vector3 bbl = new Vector3(-0.5f, -0.5f, -0.5f), bbr = new Vector3(0.5f, -0.5f, -0.5f);
            Vector3 tbl = new Vector3(-0.5f, 0.5f, -0.5f), tbr = new Vector3(0.5f, 0.5f, -0.5f);

            Quad(v, n, uv, t, bbl, bbr, bfr, bfl);   // bottom
            Quad(v, n, uv, t, bbr, bbl, tbl, tbr);   // back
            Quad(v, n, uv, t, bfl, bfr, tbr, tbl);   // slope
            Tri(v, n, uv, t, bfl, tbl, bbl);         // left
            Tri(v, n, uv, t, bfr, bbr, tbr);         // right
            return Build(v, n, uv, t);
        }

        static void Quad(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) n.Add(normal);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        static void Tri(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            for (int k = 0; k < 3; k++) n.Add(normal);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 1));
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        static readonly Dictionary<Vector3Int, Mesh> BevelCache = new Dictionary<Vector3Int, Mesh>();

        /// <summary>
        /// A box of the given size (metres) with chamfered edges and corners. The
        /// chamfer is a fixed width in metres, so it looks the same on every part:
        /// edges catch the light and parts read as machined armour, not flat blocks.
        /// </summary>
        public static Mesh BevelBox(Vector3 size)
        {
            var key = new Vector3Int(Mathf.RoundToInt(size.x * 20f), Mathf.RoundToInt(size.y * 20f), Mathf.RoundToInt(size.z * 20f));
            if (BevelCache.TryGetValue(key, out var cached) && cached != null) return cached;
            Vector3 h = new Vector3(key.x, key.y, key.z) / 40f; // half size
            float b = Mathf.Min(Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.45f, 0.22f);

            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            // Corner points: for each corner, one point on each of its three faces.
            Vector3 C(int sx, int sy, int sz, int axis)
            {
                Vector3 q = new Vector3(sx * (h.x - b), sy * (h.y - b), sz * (h.z - b));
                if (axis == 0) q.x = sx * h.x; else if (axis == 1) q.y = sy * h.y; else q.z = sz * h.z;
                return q;
            }
            // Main faces
            for (int axis = 0; axis < 3; axis++)
                for (int s = -1; s <= 1; s += 2)
                {
                    var pts = new List<Vector3>();
                    for (int a = -1; a <= 1; a += 2)
                        for (int c = -1; c <= 1; c += 2)
                        {
                            int sx = axis == 0 ? s : a, sy = axis == 1 ? s : (axis == 0 ? a : c), sz = axis == 2 ? s : c;
                            pts.Add(C(sx, sy, sz, axis));
                        }
                    Poly(v, n, uv, t, new[] { pts[0], pts[1], pts[3], pts[2] });
                }
            // Edge chamfers: along each axis, at each of the 4 edges.
            for (int axis = 0; axis < 3; axis++)
            {
                int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
                for (int s1 = -1; s1 <= 1; s1 += 2)
                    for (int s2 = -1; s2 <= 1; s2 += 2)
                    {
                        var quad = new Vector3[4];
                        int k = 0;
                        foreach (int e in new[] { -1, 1 })
                        {
                            int[] sg = new int[3];
                            sg[axis] = e; sg[a1] = s1; sg[a2] = s2;
                            quad[k++] = C(sg[0], sg[1], sg[2], a1);
                            quad[k++] = C(sg[0], sg[1], sg[2], a2);
                        }
                        Poly(v, n, uv, t, new[] { quad[0], quad[1], quad[3], quad[2] });
                    }
            }
            // Corner triangles
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        Poly(v, n, uv, t, new[] { C(sx, sy, sz, 0), C(sx, sy, sz, 1), C(sx, sy, sz, 2) });

            var m = Build(v, n, uv, t);
            m.name = "BevelBox";
            BevelCache[key] = m;
            return m;
        }

        struct RoundKey
        {
            public Vector3Int size; public int r, tx, tz;
            public override int GetHashCode() => size.GetHashCode() * 31 + r * 7 + tx * 13 + tz * 17;
            public override bool Equals(object o) => o is RoundKey k && k.size.Equals(size) && k.r == r && k.tx == tx && k.tz == tz;
        }
        static readonly Dictionary<RoundKey, Mesh> RoundCache = new Dictionary<RoundKey, Mesh>();

        /// <summary>
        /// A sculpted armour shell: a box of the given size (metres) with smoothly rounded
        /// edges of the given radius, and optionally narrower at the top (taperX, taperZ are
        /// the top face's width and depth as a fraction of the bottom's). Smooth-shaded, so
        /// big plates read as cast and machined metal instead of blocks.
        /// </summary>
        public static Mesh RoundedBox(Vector3 size, float radius, float taperX = 1f, float taperZ = 1f)
        {
            var key = new RoundKey
            {
                size = new Vector3Int(Mathf.RoundToInt(size.x * 20f), Mathf.RoundToInt(size.y * 20f), Mathf.RoundToInt(size.z * 20f)),
                r = Mathf.RoundToInt(radius * 40f), tx = Mathf.RoundToInt(taperX * 20f), tz = Mathf.RoundToInt(taperZ * 20f)
            };
            if (RoundCache.TryGetValue(key, out var cached) && cached != null) return cached;
            Vector3 h = new Vector3(key.size.x, key.size.y, key.size.z) / 40f;
            float r = Mathf.Min(key.r / 40f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f);
            float ftx = key.tx / 20f, ftz = key.tz / 20f;

            // Grid lines across each face: dense in the rounded margin, sparse in the flat middle.
            float[] Lines(float half)
            {
                float inner = half - r;
                return new[] { -half, -half + r * 0.3f, -inner, inner, half - r * 0.3f, half };
            }
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            Vector3 hin = new Vector3(h.x - r, h.y - r, h.z - r);
            for (int axis = 0; axis < 3; axis++)
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
                    float[] L1 = Lines(h[a1]), L2 = Lines(h[a2]);
                    int start = v.Count;
                    for (int j = 0; j < L2.Length; j++)
                        for (int i = 0; i < L1.Length; i++)
                        {
                            Vector3 q = Vector3.zero;
                            q[axis] = sgn * h[axis]; q[a1] = L1[i]; q[a2] = L2[j];
                            Vector3 c = new Vector3(Mathf.Clamp(q.x, -hin.x, hin.x), Mathf.Clamp(q.y, -hin.y, hin.y), Mathf.Clamp(q.z, -hin.z, hin.z));
                            Vector3 nn = (q - c).normalized;
                            Vector3 pos = c + nn * r;
                            // Taper towards the top.
                            float f = (pos.y / h.y + 1f) * 0.5f;
                            float sx = Mathf.Lerp(1f, ftx, f), sz = Mathf.Lerp(1f, ftz, f);
                            pos.x *= sx; pos.z *= sz;
                            v.Add(pos);
                            n.Add(nn);
                            uv.Add(new Vector2(q[a1], q[a2]));
                        }
                    int w = L1.Length;
                    for (int j = 0; j < L2.Length - 1; j++)
                        for (int i = 0; i < w - 1; i++)
                        {
                            int a = start + j * w + i, b = a + 1, c = a + w, d = c + 1;
                            // Wind outward: check against the face normal.
                            Vector3 cr = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                            Vector3 fn = Vector3.zero; fn[axis] = sgn;
                            if (Vector3.Dot(cr, fn) >= 0f) { t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c); }
                            else { t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d); }
                        }
                }
            var m = new Mesh();
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            if (ftx != 1f || ftz != 1f) SmoothNormals(m, v, t);
            else m.SetNormals(n);
            m.RecalculateBounds();
            m.RecalculateTangents();
            m.name = "RoundedBox";
            RoundCache[key] = m;
            return m;
        }

        /// <summary>Smooth normals that ignore seams between separately built faces (welds by position).</summary>
        static void SmoothNormals(Mesh m, List<Vector3> v, List<int> t)
        {
            var acc = new Dictionary<Vector3Int, Vector3>();
            Vector3Int K(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.y * 1000f), Mathf.RoundToInt(p.z * 1000f));
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 fn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                for (int k = 0; k < 3; k++)
                {
                    var key = K(v[t[i + k]]);
                    acc.TryGetValue(key, out var cur);
                    acc[key] = cur + fn;
                }
            }
            var n = new List<Vector3>(v.Count);
            foreach (var p in v) n.Add(acc[K(p)].normalized);
            m.SetNormals(n);
        }

        static readonly Dictionary<Vector3Int, Mesh> DiscCache = new Dictionary<Vector3Int, Mesh>();

        /// <summary>
        /// A machined disc along local Y (diameter, height in metres): chamfered rims and,
        /// on both faces, a recessed ring around a raised hub. Big joints and wheels.
        /// </summary>
        public static Mesh Disc(float diameter, float height)
        {
            var key = new Vector3Int(Mathf.RoundToInt(diameter * 20f), Mathf.RoundToInt(height * 20f), 0);
            if (DiscCache.TryGetValue(key, out var cached) && cached != null) return cached;
            float R = key.x / 40f, H = key.y / 40f;
            float b = Mathf.Min(R * 0.12f, H * 0.4f);
            // Profile from the axis outwards over the top face, down the rim, back over the bottom.
            // (radius, y) pairs; the hub is raised, the ring around it recessed.
            float hubR = R * 0.42f, ringIn = R * 0.5f, ringOut = R * 0.78f, rec = Mathf.Min(H * 0.18f, 0.12f);
            var prof = new List<Vector2>
            {
                new Vector2(0f, H + rec * 0.6f), new Vector2(hubR - 0.02f, H + rec * 0.6f), new Vector2(hubR, H),
                new Vector2(ringIn, H - rec), new Vector2(ringOut, H - rec), new Vector2(ringOut + 0.01f, H),
                new Vector2(R - b, H), new Vector2(R, H - b), new Vector2(R, -H + b), new Vector2(R - b, -H),
                new Vector2(ringOut + 0.01f, -H), new Vector2(ringOut, -H + rec), new Vector2(ringIn, -H + rec),
                new Vector2(hubR, -H), new Vector2(hubR - 0.02f, -H - rec * 0.6f), new Vector2(0f, -H - rec * 0.6f),
            };
            const int seg = 16;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            // Each profile edge becomes its own band (hard edges between bands).
            for (int e = 0; e < prof.Count - 1; e++)
            {
                Vector2 p0 = prof[e], p1 = prof[e + 1];
                float dx = p1.x - p0.x, dy = p1.y - p0.y;
                float dl = Mathf.Sqrt(dx * dx + dy * dy);
                if (dl < 1e-4f) continue;
                Vector2 pn = new Vector2(dy / dl, -dx / dl); // outward in (r, y)
                int start = v.Count;
                for (int s = 0; s <= seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    v.Add(new Vector3(c * p0.x, p0.y, sn * p0.x));
                    v.Add(new Vector3(c * p1.x, p1.y, sn * p1.x));
                    Vector3 nn = new Vector3(c * pn.x, pn.y, sn * pn.x).normalized;
                    n.Add(nn); n.Add(nn);
                    uv.Add(new Vector2(c * p0.x, sn * p0.x)); uv.Add(new Vector2(c * p1.x, sn * p1.x));
                }
                for (int s = 0; s < seg; s++)
                {
                    int i0 = start + s * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                    Vector3 cr = Vector3.Cross(v[i1] - v[i0], v[i2] - v[i0]);
                    Vector3 mid = (v[i0] + v[i1] + v[i2]) / 3f;
                    Vector3 nn = n[i0];
                    if (Vector3.Dot(cr, nn) >= 0f) { t.Add(i0); t.Add(i1); t.Add(i2); t.Add(i1); t.Add(i3); t.Add(i2); }
                    else { t.Add(i0); t.Add(i2); t.Add(i1); t.Add(i1); t.Add(i2); t.Add(i3); }
                }
            }
            var m = Build(v, n, uv, t);
            m.name = "Disc";
            DiscCache[key] = m;
            return m;
        }

        /// <summary>A flexible hose through the given points (smooth Catmull-Rom curve).</summary>
        public static Mesh Tube(IList<Vector3> pts, float radius, int sides = 8)
        {
            var path = new List<Vector3>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
                for (int k = 0; k < 6; k++)
                {
                    float u = k / 6f, u2 = u * u, u3 = u2 * u;
                    path.Add(0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3));
                }
            }
            path.Add(pts[pts.Count - 1]);
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            Vector3 prevUp = Vector3.up;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 fwd = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
                Vector3 right = Vector3.Cross(prevUp, fwd);
                if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(Vector3.right, fwd);
                right.Normalize();
                Vector3 up = Vector3.Cross(fwd, right);
                prevUp = up;
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 d = right * Mathf.Cos(a) + up * Mathf.Sin(a);
                    v.Add(path[i] + d * radius); n.Add(d); uv.Add(new Vector2(s / (float)sides, i * 0.2f));
                }
            }
            int w = sides + 1;
            for (int i = 0; i < path.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = i * w + s, b = a + 1, c = a + w, d = c + 1;
                    Vector3 cr = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                    if (Vector3.Dot(cr, n[a]) >= 0f) { t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c); }
                    else { t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d); }
                }
            var m = Build(v, n, uv, t);
            m.name = "Tube";
            return m;
        }

        /// <summary>Adds a flat convex face, wound so it faces away from the origin.</summary>
        static void Poly(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3[] p)
        {
            Vector3 centre = Vector3.zero;
            foreach (var q in p) centre += q;
            centre /= p.Length;
            Vector3 normal = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
            if (normal.sqrMagnitude < 1e-10f) return;
            normal.Normalize();
            bool flip = Vector3.Dot(normal, centre) < 0f;
            if (flip) normal = -normal;
            int i = v.Count;
            Vector3 up = Mathf.Abs(normal.y) > 0.7f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(up, normal).normalized;
            up = Vector3.Cross(normal, right);
            foreach (var q in p)
            {
                v.Add(q);
                n.Add(normal);
                uv.Add(new Vector2(Vector3.Dot(q, right), Vector3.Dot(q, up)));
            }
            for (int k = 1; k < p.Length - 1; k++)
            {
                // Same convention as Box(): Cross(b - a, c - a) points out of the front face.
                if (flip) { t.Add(i); t.Add(i + k + 1); t.Add(i + k); }
                else { t.Add(i); t.Add(i + k); t.Add(i + k + 1); }
            }
        }

        static Mesh Build(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh();
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }
}
