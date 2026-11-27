using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RobotArena
{
    /// <summary>
    /// Combines all the small primitive parts under a container into one mesh per
    /// material. Robots are built from hundreds of parts; merging them keeps the
    /// draw-call count low enough for phones and the browser.
    /// </summary>
    public static class MeshMerge
    {
        static readonly Dictionary<Mesh, Mesh> mirrored = new Dictionary<Mesh, Mesh>();

        /// <summary>A copy of the mesh's first submesh with every triangle's corner order reversed.</summary>
        static Mesh Mirrored(Mesh src)
        {
            if (mirrored.TryGetValue(src, out var m) && m != null) return m;
            m = new Mesh { name = src.name + " (mirrored)", indexFormat = src.indexFormat };
            m.vertices = src.vertices;
            m.normals = src.normals;
            m.uv = src.uv;
            var t = src.GetTriangles(0);
            for (int i = 0; i < t.Length; i += 3) { int a = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = a; }
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            mirrored[src] = m;
            return m;
        }

        public static void Merge(Transform container)
        {
            if (container == null) return;

            var groups = new Dictionary<Material, List<CombineInstance>>();
            var toClear = new List<GameObject>();
            Matrix4x4 toLocal = container.worldToLocalMatrix;

            foreach (var mf in container.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.transform == container) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null) continue;
                var mat = mr.sharedMaterial;
                if (mat == null) continue;

                if (!groups.TryGetValue(mat, out var list))
                {
                    list = new List<CombineInstance>();
                    groups[mat] = list;
                }
                var m = toLocal * mf.transform.localToWorldMatrix;
                list.Add(new CombineInstance
                {
                    // A mirrored part (negative scale) would come out inside out once baked:
                    // CombineMeshes moves the corners but keeps their order.
                    mesh = m.determinant < 0f ? Mirrored(mf.sharedMesh) : mf.sharedMesh,
                    subMeshIndex = 0,
                    transform = m
                });
                toClear.Add(mf.gameObject);
            }

            foreach (var kv in groups)
            {
                var merged = new GameObject("Merged " + kv.Key.name);
                merged.transform.SetParent(container, false);
                var mesh = new Mesh { name = "Merged" };
                int verts = 0;
                foreach (var ci in kv.Value) verts += ci.mesh.vertexCount;
                if (verts > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                merged.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = merged.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key;
                r.shadowCastingMode = ShadowCastingMode.On;
            }

            foreach (var go in toClear)
            {
                if (go == null) continue;
                Object.Destroy(go.GetComponent<MeshRenderer>());
                Object.Destroy(go.GetComponent<MeshFilter>());
            }
        }
    }
}
