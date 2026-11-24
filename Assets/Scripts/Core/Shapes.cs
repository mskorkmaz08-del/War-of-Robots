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
