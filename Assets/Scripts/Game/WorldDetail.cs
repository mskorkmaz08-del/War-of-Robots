using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RobotArena
{
    /// <summary>
    /// Puts the real-size world (WorldGen) into the scene: plans roads and towns before
    /// the ground is sculpted, builds the base defences, then the props as a few big
    /// meshes on the finished ground, and lets robots crush them.
    /// </summary>
    public static class WorldDetail
    {
        static GameAssets A => GameAssets.Get();
        static Material propMat;

        public static WorldGen.Plan Plan(MapId map, MapLayout layout)
        {
            var plan = WorldGen.MakePlan(map, layout, Frontier.Half);
            foreach (var f in plan.flats) BattleGround.FlatRects.Add(new GroundShape.FlatRect { min = f.min, max = f.max });
            foreach (var r in plan.roads) BattleGround.Roads.Add(new GroundShape.Road { a = r.a, b = r.b, width = r.width });
            return plan;
        }

        // ---------------------------------------------------------------------
        // Bases: hangars, a control tower and concrete blast walls in front
        // ---------------------------------------------------------------------

        public static void BuildBases(Transform root, MapId map, MapLayout l)
        {
            var a = A;
            if (a == null || l == null) return;
            int seed = 4000;
            for (int team = 0; team < 2; team++)
            {
                Vector3 b = l.Base(team);
                float dir = b.z < 0f ? 1f : -1f;      // towards the middle
                float face = dir > 0f ? 180f : 0f;   // building fronts (local -z) face the middle

                // Two big hangars on the flanks of the base.
                foreach (float sx in new[] { -1f, 1f })
                {
                    var size = new Vector3(46f, 18f, 30f);
                    Vector3 p = b + new Vector3(sx * 118f, 0f, -dir * 14f);
                    Prim(root, p + Vector3.up * size.y * 0.5f, face, size, a.buildingMat);
                    BuildingKit.Dress(root, p, face, size, BuildingKit.Style.Industrial, seed++);
                }
                // Control tower behind the slots.
                {
                    var size = new Vector3(10f, 34f, 10f);
                    Vector3 p = b + new Vector3(-40f, 0f, -dir * 48f);
                    Prim(root, p + Vector3.up * size.y * 0.5f, face, size, a.wallMat);
                    BuildingKit.Dress(root, p, face, size, BuildingKit.Style.Office, seed++);
                    Prim(root, p + Vector3.up * (size.y + 2.5f), face, new Vector3(14f, 5f, 14f), a.buildingMat);
                    Prim(root, p + Vector3.up * (size.y + 8f), 0f, new Vector3(0.6f, 6f, 0.6f), a.hazardMat);
                }
                // Blast walls in front: 9 m tall, gaps for the road and to walk out.
                foreach (float x in new[] { -150f, -100f, -52f, 52f, 100f, 150f })
                {
                    Vector3 p = b + new Vector3(x, 0f, dir * 62f);
                    Prim(root, p + Vector3.up * 4.5f, 0f, new Vector3(34f, 9f, 2.4f), a.wallMat);
                    Prim(root, p + new Vector3(0f, 1.5f, -dir * 1.6f), 0f, new Vector3(34f, 3f, 1.2f), a.wallMat);
                    Prim(root, p + Vector3.up * 9.15f, 0f, new Vector3(34.4f, 0.3f, 2.8f), a.hazardMat);
                }
                // Fuel tanks behind the hangars.
                foreach (float sx in new[] { -1f, 1f })
                    Prim(root, b + new Vector3(sx * 150f, 7f, -dir * 52f), 0f, new Vector3(16f, 7f, 16f), a.wallMat, PrimitiveType.Cylinder);

                // Reserve bases: a bunker and two walls each.
                foreach (var r in l.Reserve(team))
                {
                    var size = new Vector3(18f, 8f, 12f);
                    Vector3 p = r + new Vector3(Mathf.Sign(r.x) * 34f, 0f, -dir * 8f);
                    Prim(root, p + Vector3.up * size.y * 0.5f, face, size, a.buildingMat);
                    BuildingKit.Dress(root, p, face, size, BuildingKit.Style.Bunker, seed++);
                    Prim(root, r + new Vector3(-12f, 4f, dir * 40f), 0f, new Vector3(28f, 8f, 2.4f), a.wallMat);
                }
            }
        }

        static GameObject Prim(Transform parent, Vector3 pos, float yaw, Vector3 scale, Material mat, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // ---------------------------------------------------------------------
        // Props
        // ---------------------------------------------------------------------

        static readonly Collider[] probe = new Collider[16];

        /// <summary>Is a map building (with a collider) already standing here?</summary>
        static bool Blocked(Vector3 q, float radius)
        {
            int n = Physics.OverlapSphereNonAlloc(q + Vector3.up * (radius + 1.5f), radius, probe, ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = probe[i];
                if (c == null) continue;
                var b = c.bounds;
                if (b.size.x > 300f || b.size.z > 300f || c.name == "Mountains" || c.name == "BattleGround") continue;
                return true;
            }
            return false;
        }

        public static void Build(WorldGen.Plan plan)
        {
            if (plan == null || A == null) return;
            Physics.SyncTransforms();
            var world = new PropWorld();
            WorldGen.Generate(plan, world, BattleGround.HeightAt, Blocked);

            var root = new GameObject("WorldDetail");
            var mat = PropMaterial();
            var crush = root.AddComponent<Crushables>();
            var meshes = new Dictionary<long, Crushables.Chunk>();
            foreach (var kv in world.chunks)
            {
                if (kv.Value.Count == 0) continue;
                var mesh = kv.Value.ToMesh("Props_" + kv.Key);
                var go = new GameObject("Props");
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                meshes[kv.Key] = new Crushables.Chunk
                {
                    mesh = mesh,
                    verts = kv.Value.v.ToArray(),
                    norms = kv.Value.n.ToArray(),
                    cols = kv.Value.c.ToArray(),
                    tris = kv.Value.t.ToArray(),
                };
                mesh.MarkDynamic();
            }

            // Big buildings stop robots and shots (until they're brought down).
            var solids = new GameObject("Buildings");
            solids.transform.SetParent(root.transform, false);
            var colliders = new Dictionary<int, Collider>();
            foreach (var s in world.solids)
            {
                var go = new GameObject("Building");
                go.transform.SetParent(solids.transform, false);
                go.transform.SetPositionAndRotation(s.centre, Quaternion.Euler(0f, s.yaw, 0f));
                var box = go.AddComponent<BoxCollider>();
                box.size = s.size;
                if (s.crush >= 0) colliders[s.crush] = box;
            }
            crush.Init(meshes, world.crush, colliders);
            Rubble.Create(mat);
            Fx.DustTint = DustFor(plan.biome);
            Debug.Log("[WorldDetail] " + world.chunks.Count + " chunks, " + world.VertexCount + " vertices, " + world.crush.Count + " crushable, " + world.solids.Count + " buildings");
        }

        static Material PropMaterial()
        {
            if (propMat != null) return propMat;
            var shader = Shader.Find("RobotArena/Props");
            var a = A;
            if (shader != null)
            {
                propMat = new Material(shader) { name = "Props" };
                var src = a.wallMat;
                if (src != null)
                {
                    if (src.HasProperty("_MainTex")) propMat.SetTexture("_MainTex", src.GetTexture("_MainTex"));
                    if (src.HasProperty("_MaskTex")) propMat.SetTexture("_MaskTex", src.GetTexture("_MaskTex"));
                }
                propMat.SetFloat("_Tiling", 3f);
                propMat.enableInstancing = false;
            }
            else
            {
                // Without the props shader: a stock shader that still shows vertex colours.
                var fallback = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
                propMat = fallback != null ? new Material(fallback) : new Material(a.buildingMat);
                Debug.LogWarning("[WorldDetail] Props shader missing, using " + propMat.shader.name);
            }
            return propMat;
        }

        /// <summary>
        /// Explosions knock over trees and poles, wreck cars and houses nearby, and
        /// damage big buildings, which come down when they've taken enough.
        /// </summary>
        public static void Blast(Vector3 pos, float radius, float damage = 0f)
        {
            if (Crushables.Instance != null) Crushables.Instance.Blast(pos, radius, damage);
        }

        /// <summary>Colour of the dust kicked up on each kind of map.</summary>
        static Color DustFor(Biome b)
        {
            switch (b)
            {
                case Biome.Desert: return new Color(0.66f, 0.55f, 0.42f);
                case Biome.Snow: return new Color(0.78f, 0.8f, 0.83f);
                case Biome.Ruined: return new Color(0.44f, 0.42f, 0.4f);
                default: return new Color(0.5f, 0.46f, 0.4f);
            }
        }
    }
}
