using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RobotArena
{
    /// <summary>
    /// Builds one big vertex-coloured mesh out of many small shapes (houses, cars,
    /// trees...). Shapes are given in the local frame set by <see cref="At"/>.
    /// Every face has its own vertices (flat shading), so a crushed car or a fallen
    /// tree can be moved later without disturbing its neighbours.
    /// </summary>
    public class PropMesh
    {
        public readonly List<Vector3> v = new List<Vector3>(4096);
        public readonly List<Vector3> n = new List<Vector3>(4096);
        public readonly List<Color32> c = new List<Color32>(4096);
        public readonly List<int> t = new List<int>(8192);

        Matrix4x4 m = Matrix4x4.identity;
        Quaternion rot = Quaternion.identity;

        public int Count => v.Count;

        /// <summary>Sets the frame for the next shapes: position and heading (degrees), optional tilt.</summary>
        public void At(Vector3 pos, float yaw, float pitch = 0f, float roll = 0f)
        {
            rot = Quaternion.Euler(pitch, yaw, roll);
            m = Matrix4x4.TRS(pos, rot, Vector3.one);
        }

        Vector3 P(Vector3 local) => m.MultiplyPoint3x4(local);

        /// <summary>Colour with smoothness in alpha (0 matte .. 1 glossy).</summary>
        public static Color32 Col(Color col, float gloss = 0.1f)
        {
            return new Color32((byte)(Mathf.Clamp01(col.r) * 255f), (byte)(Mathf.Clamp01(col.g) * 255f), (byte)(Mathf.Clamp01(col.b) * 255f), (byte)(Mathf.Clamp01(gloss) * 255f));
        }

        /// <summary>A quad from four world-space corners (counter-clockwise seen from the front).</summary>
        public void QuadW(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color32 col)
        {
            Vector3 nrm = Vector3.Cross(b - a, d - a);
            if (nrm.sqrMagnitude < 1e-10f) nrm = Vector3.up;
            nrm.Normalize();
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
            for (int k = 0; k < 4; k++) { n.Add(nrm); c.Add(col); }
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        public void TriW(Vector3 a, Vector3 b, Vector3 cc, Color32 col)
        {
            Vector3 nrm = Vector3.Cross(cc - a, b - a);
            if (nrm.sqrMagnitude < 1e-10f) nrm = Vector3.up;
            nrm.Normalize();
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(cc);
            for (int k = 0; k < 3; k++) { n.Add(nrm); c.Add(col); }
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
        }

        /// <summary>Local-space quad.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color32 col) => QuadW(P(a), P(b), P(cc), P(d), col);
        public void Tri(Vector3 a, Vector3 b, Vector3 cc, Color32 col) => TriW(P(a), P(b), P(cc), col);

        /// <summary>Axis-aligned (in the local frame) box. bottom = false skips the underside.</summary>
        public void Box(Vector3 centre, Vector3 size, Color32 col, bool bottom = false)
        {
            Vector3 h = size * 0.5f;
            Vector3 c0 = centre;
            Vector3 p000 = c0 + new Vector3(-h.x, -h.y, -h.z), p100 = c0 + new Vector3(h.x, -h.y, -h.z);
            Vector3 p010 = c0 + new Vector3(-h.x, h.y, -h.z), p110 = c0 + new Vector3(h.x, h.y, -h.z);
            Vector3 p001 = c0 + new Vector3(-h.x, -h.y, h.z), p101 = c0 + new Vector3(h.x, -h.y, h.z);
            Vector3 p011 = c0 + new Vector3(-h.x, h.y, h.z), p111 = c0 + new Vector3(h.x, h.y, h.z);
            Quad(p000, p010, p110, p100, col); // -z
            Quad(p101, p111, p011, p001, col); // +z
            Quad(p001, p011, p010, p000, col); // -x
            Quad(p100, p110, p111, p101, col); // +x
            Quad(p010, p011, p111, p110, col); // top
            if (bottom) Quad(p001, p000, p100, p101, col);
        }

        /// <summary>A box with its own extra rotation (degrees) inside the frame.</summary>
        public void BoxR(Vector3 centre, Vector3 size, Vector3 euler, Color32 col)
        {
            var saved = m;
            m = m * Matrix4x4.TRS(centre, Quaternion.Euler(euler), Vector3.one);
            Box(Vector3.zero, size, col, true);
            m = saved;
        }

        /// <summary>A thin flat panel on a wall: windows, doors, signs. normal = local facing direction.</summary>
        public void Panel(Vector3 centre, Vector3 right, Vector3 up, float w, float h, Color32 col)
        {
            Vector3 r = right * (w * 0.5f), u = up * (h * 0.5f);
            Quad(centre - r - u, centre - r + u, centre + r + u, centre + r - u, col);
        }

        /// <summary>A window/door/sign on a wall that faces <paramref name="facing"/> (local).</summary>
        public void Face(Vector3 centre, Vector3 facing, float w, float h, Color32 col)
        {
            Vector3 right = Vector3.Cross(facing, Vector3.up).normalized;
            Panel(centre + facing * 0.04f, right, Vector3.up, w, h, col);
        }

        /// <summary>Gable roof along local x: ridge at height h above y0, eaves width d, length w.</summary>
        public void Gable(Vector3 centre, float w, float d, float h, Color32 roof, Color32 gable)
        {
            float hx = w * 0.5f, hz = d * 0.5f;
            Vector3 e0 = centre + new Vector3(-hx, 0f, -hz), e1 = centre + new Vector3(hx, 0f, -hz);
            Vector3 e2 = centre + new Vector3(hx, 0f, hz), e3 = centre + new Vector3(-hx, 0f, hz);
            Vector3 r0 = centre + new Vector3(-hx, h, 0f), r1 = centre + new Vector3(hx, h, 0f);
            Quad(e0, r0, r1, e1, roof);
            Quad(e2, r1, r0, e3, roof);
            Tri(e0, r0, e3, gable);
            Tri(e2, r1, e1, gable);
            // Underside of the eaves so the roof isn't see-through from below.
            Quad(e0, e1, e2, e3, gable);
        }

        /// <summary>Hip roof (four slopes) for square-ish houses.</summary>
        public void Hip(Vector3 centre, float w, float d, float h, Color32 roof)
        {
            float hx = w * 0.5f, hz = d * 0.5f;
            float ridge = Mathf.Max(0f, hx - hz);
            Vector3 e0 = centre + new Vector3(-hx, 0f, -hz), e1 = centre + new Vector3(hx, 0f, -hz);
            Vector3 e2 = centre + new Vector3(hx, 0f, hz), e3 = centre + new Vector3(-hx, 0f, hz);
            Vector3 r0 = centre + new Vector3(-ridge, h, 0f), r1 = centre + new Vector3(ridge, h, 0f);
            Quad(e0, r0, r1, e1, roof);
            Quad(e2, r1, r0, e3, roof);
            Tri(e0, r0, e3, roof);
            Tri(e2, r1, e1, roof);
        }

        /// <summary>Vertical cylinder or cone (r1 = 0) standing on basePos.</summary>
        public void Cyl(Vector3 basePos, float r0, float r1, float h, int sides, Color32 col, bool cap = true)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 b0 = basePos + d0 * r0, b1 = basePos + d1 * r0;
                Vector3 t0 = basePos + Vector3.up * h + d0 * r1, t1 = basePos + Vector3.up * h + d1 * r1;
                if (r1 <= 0.001f) Tri(b1, t0, b0, col);
                else Quad(b0, t0, t1, b1, col);
                if (cap && r1 > 0.001f) Tri(basePos + Vector3.up * h, t0, t1, col);
            }
        }

        /// <summary>
        /// A smooth-shaded cone (shared vertices: a ring and the tip), for spruce tiers.
        /// droop lowers the rim so a tier hangs like a branch skirt.
        /// </summary>
        public void ConeSmooth(Vector3 basePos, float r, float h, int sides, Color32 col, float droop = 0f, System.Random rnd = null)
        {
            int i0 = v.Count;
            float slope = r / Mathf.Max(0.01f, h);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float rr = r * (rnd != null ? 0.88f + (float)rnd.NextDouble() * 0.24f : 1f);
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(P(basePos + d * rr + Vector3.down * droop));
                n.Add(rot * (d + Vector3.up * slope).normalized);
                c.Add(col);
            }
            v.Add(P(basePos + Vector3.up * h));
            n.Add(rot * Vector3.up);
            c.Add(col);
            int tip = v.Count - 1;
            for (int i = 0; i < sides; i++)
            {
                int a = i0 + i, b = i0 + (i + 1) % sides;
                t.Add(a); t.Add(tip); t.Add(b);
            }
        }

        /// <summary>Horizontal cylinder along local x (wheels, pipes, tanks on their side).</summary>
        public void CylX(Vector3 centre, float r, float len, int sides, Color32 col, bool caps = true)
        {
            float hx = len * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = new Vector3(0f, Mathf.Cos(a0), Mathf.Sin(a0)) * r, d1 = new Vector3(0f, Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                Vector3 L = new Vector3(-hx, 0f, 0f), R = new Vector3(hx, 0f, 0f);
                Quad(centre + L + d0, centre + L + d1, centre + R + d1, centre + R + d0, col);
                if (!caps) continue;
                Tri(centre + R, centre + R + d1, centre + R + d0, col);
                Tri(centre + L, centre + L + d0, centre + L + d1, col);
            }
        }

        static readonly Vector3[] ico;
        static readonly int[] icoTris =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };

        static PropMesh()
        {
            float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
            ico = new[]
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1)
            };
            for (int i = 0; i < ico.Length; i++) ico[i] = ico[i].normalized;
        }

        /// <summary>A lumpy low-poly ball (foliage, bushes, rocks, rubble).</summary>
        public void Blob(Vector3 centre, Vector3 radii, Color32 col, System.Random rnd, float lumpy = 0.18f)
        {
            var p = new Vector3[12];
            for (int i = 0; i < 12; i++)
            {
                float j = 1f + ((float)rnd.NextDouble() * 2f - 1f) * lumpy;
                p[i] = centre + Vector3.Scale(ico[i], radii) * j;
            }
            for (int i = 0; i < icoTris.Length; i += 3)
            {
                // Slight shade variation per face reads as leaves catching light.
                float k = 0.88f + (float)rnd.NextDouble() * 0.24f;
                var cc = new Color32((byte)Mathf.Min(255, col.r * k), (byte)Mathf.Min(255, col.g * k), (byte)Mathf.Min(255, col.b * k), col.a);
                Tri(p[icoTris[i]], p[icoTris[i + 2]], p[icoTris[i + 1]], cc);
            }
        }

        static Vector3[] sphere;
        static int[] sphereTris;

        /// <summary>An icosphere subdivided once: 42 shared vertices, 80 faces.</summary>
        static void BuildSphere()
        {
            if (sphere != null) return;
            var verts = new List<Vector3>(ico);
            var tris = new List<int>();
            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 16) | (long)b : ((long)b << 16) | (long)a;
                if (mid.TryGetValue(key, out int i)) return i;
                i = verts.Count;
                verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
                mid[key] = i;
                return i;
            }
            for (int i = 0; i < icoTris.Length; i += 3)
            {
                int a = icoTris[i], b = icoTris[i + 1], c = icoTris[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                tris.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
            }
            sphere = verts.ToArray();
            sphereTris = tris.ToArray();
        }

        /// <summary>
        /// A soft, rounded mass of leaves: smooth-shaded so it reads as foliage rather
        /// than a faceted rock, lumpy outline, lighter on top and darker underneath
        /// (where the leaves shade each other), with a little colour mottling.
        /// </summary>
        public void Foliage(Vector3 centre, Vector3 radii, Color32 col, System.Random rnd, float lumpy = 0.16f, bool coarse = false)
        {
            BuildSphere();
            // coarse: the plain icosahedron (12 vertices), for trees seen from afar.
            var sv = coarse ? ico : sphere;
            var st = coarse ? icoTris : sphereTris;
            int i0 = v.Count;
            // A few big bumps so the outline isn't a perfect ball.
            var bumps = new Vector3[3];
            for (int k = 0; k < 3; k++)
                bumps[k] = new Vector3((float)rnd.NextDouble() * 2f - 1f, (float)rnd.NextDouble() * 1.2f - 0.4f, (float)rnd.NextDouble() * 2f - 1f).normalized;
            for (int k = 0; k < sv.Length; k++)
            {
                Vector3 u = sv[k];
                float j = 1f + ((float)rnd.NextDouble() * 2f - 1f) * lumpy;
                for (int b = 0; b < 3; b++) j += Mathf.Max(0f, Vector3.Dot(u, bumps[b]) - 0.55f) * 0.5f;
                Vector3 local = centre + Vector3.Scale(u, radii) * j;
                v.Add(P(local));
                Vector3 nn = new Vector3(u.x / radii.x, u.y / radii.y, u.z / radii.z).normalized;
                n.Add(rot * nn);
                float shade = 0.6f + 0.42f * (u.y * 0.5f + 0.5f) + ((float)rnd.NextDouble() - 0.5f) * 0.12f;
                float warm = ((float)rnd.NextDouble() - 0.5f) * 0.08f;
                c.Add(new Color32((byte)Mathf.Clamp(col.r * (shade + warm), 0f, 255f), (byte)Mathf.Clamp(col.g * shade, 0f, 255f), (byte)Mathf.Clamp(col.b * (shade - warm), 0f, 255f), col.a));
            }
            for (int k = 0; k < st.Length; k += 3)
            {
                t.Add(i0 + st[k]);
                t.Add(i0 + st[k + 1]);
                t.Add(i0 + st[k + 2]);
            }
        }

        /// <summary>A straight beam between two local points (wires, pipes, railings), square section.</summary>
        public void Beam(Vector3 a, Vector3 b, float thick, Color32 col)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return;
            var saved = m;
            m = m * Matrix4x4.TRS((a + b) * 0.5f, Quaternion.LookRotation(d / len, Mathf.Abs(d.y / len) > 0.95f ? Vector3.forward : Vector3.up), Vector3.one);
            // Four long sides only: the ends of a thin beam are never seen.
            float h = thick * 0.5f, l = len * 0.5f;
            Vector3 p000 = new Vector3(-h, -h, -l), p100 = new Vector3(h, -h, -l), p010 = new Vector3(-h, h, -l), p110 = new Vector3(h, h, -l);
            Vector3 p001 = new Vector3(-h, -h, l), p101 = new Vector3(h, -h, l), p011 = new Vector3(-h, h, l), p111 = new Vector3(h, h, l);
            Quad(p001, p011, p010, p000, col); // -x
            Quad(p100, p110, p111, p101, col); // +x
            Quad(p010, p011, p111, p110, col); // top
            Quad(p001, p000, p100, p101, col); // bottom
            m = saved;
        }

        /// <summary>A flat strip seen from both sides (wires, cables): 8 vertices.</summary>
        public void Ribbon(Vector3 a, Vector3 b, float width, Color32 col)
        {
            Vector3 d = b - a;
            Vector3 side = Vector3.Cross(d, Vector3.up).normalized * (width * 0.5f);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.right * (width * 0.5f);
            Vector3 up = Vector3.Cross(side, d).normalized * (width * 0.5f);
            Quad(a - up, b - up, b + up, a + up, col);
            Quad(a + up, b + up, b - up, a - up, col);
        }

        /// <summary>Makes a mesh (32-bit indices when needed).</summary>
        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
