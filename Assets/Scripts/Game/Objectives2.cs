using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Blender-made scenery and objective models (Tools/mechs/structures.py), stored in the
    /// same RBM3 format as the robots: Resources/RobotModels/&lt;Name&gt;.bytes. Each model
    /// uses the ten material slots (Primary, Secondary, Dark, Metal, Chrome, Accent, Glow,
    /// Black, Rubber, Hot), filled here with scenery paints instead of a robot livery.
    /// </summary>
    public static class SceneryModels
    {
        static readonly Dictionary<string, Material[]> palettes = new Dictionary<string, Material[]>();

        /// <summary>Paints for a model: the main colour, a second colour and the glow colour.</summary>
        public static Material[] Palette(Color primary, Color secondary, Color glow)
        {
            string key = primary.ToString() + secondary + glow;
            if (palettes.TryGetValue(key, out var p) && p[0] != null) return p;
            var a = GameAssets.Get();
            p = new[]
            {
                a.Paint(primary, 0.45f, 0.45f, true),
                a.Paint(secondary, 0.4f, 0.45f, true),
                a.Paint(new Color(0.16f, 0.17f, 0.19f), 0.7f, 0.4f, true),
                a.Paint(new Color(0.42f, 0.43f, 0.45f), 0.85f, 0.5f, false),
                a.Paint(new Color(0.75f, 0.76f, 0.78f), 0.95f, 0.8f, false),
                // Accent is safety-yellow paint: hazard stripes are modelled as geometry.
                a.Paint(new Color(0.95f, 0.72f, 0.12f), 0.3f, 0.45f, false),
                a.Glow(glow, 2.6f),
                a.Paint(new Color(0.05f, 0.05f, 0.06f), 0.3f, 0.3f, false),
                a.Paint(new Color(0.09f, 0.09f, 0.09f), 0f, 0.2f, false),
                a.Glow(new Color(1f, 0.45f, 0.12f), 3.2f),
            };
            palettes[key] = p;
            return p;
        }

        /// <summary>
        /// Places every part of a model under parent. With collide, each part gets a mesh
        /// collider so robots and shots treat it as solid. Returns false when the build has no
        /// such model (the caller builds a stand-in then).
        /// </summary>
        public static bool Place(Transform parent, string name, Material[] mats, bool collide, float scale = 1f)
        {
            var m = RobotModel.Load(name);
            if (m == null || m.info == null || m.info.frames == null) return false;
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * scale;
            // A model with "Col" parts carries its own simple physics hull; then the drawn
            // parts get no colliders, which keeps physics cheap on phones.
            bool hull = false;
            foreach (var f in m.info.frames) if (f.name.StartsWith("Col")) { hull = true; break; }
            foreach (var f in m.info.frames)
            {
                if (!m.parts.TryGetValue(f.name, out var part)) continue;
                var go = new GameObject(f.name);
                go.transform.SetParent(root, false);
                go.transform.localPosition = f.Origin;
                go.transform.localRotation = f.Rotation;
                if (f.name.StartsWith("Col"))
                {
                    if (collide) go.AddComponent<MeshCollider>().sharedMesh = part.mesh;
                    continue;
                }
                go.AddComponent<MeshFilter>().sharedMesh = part.mesh;
                var r = go.AddComponent<MeshRenderer>();
                var list = new Material[part.materials.Length];
                for (int i = 0; i < list.Length; i++) list[i] = mats[Mathf.Clamp(part.materials[i], 0, mats.Length - 1)];
                r.sharedMaterials = list;
                // Glow-only parts (lamps, screens) don't need a collider of their own.
                if (collide && !hull && part.mesh.vertexCount > 24 && !f.name.StartsWith("NoCol"))
                    go.AddComponent<MeshCollider>().sharedMesh = part.mesh;
            }
            return true;
        }
    }

    public static partial class Structures
    {
        /// <summary>
        /// A base power core for Siege and the sabotage missions: a squat armoured generator
        /// with a glowing fusion column, radiator fins and cable trunks.
        /// </summary>
        public static Damageable PowerCore(Vector3 pos, int team, float yaw)
        {
            var go = new GameObject(team == 0 ? "Our Power Core" : "Enemy Power Core");
            go.transform.SetPositionAndRotation(new Vector3(pos.x, BattleGround.HeightAt(pos.x, pos.z), pos.z), Quaternion.Euler(0f, yaw, 0f));
            var t = go.transform;
            Color teamGlow = team == 0 ? UiKit.Cyan : UiKit.Red;
            var mats = SceneryModels.Palette(new Color(0.58f, 0.6f, 0.6f), team == 0 ? new Color(0.22f, 0.38f, 0.48f) : new Color(0.5f, 0.2f, 0.16f), teamGlow);
            if (!SceneryModels.Place(t, "StructPowerCore", mats, true))
            {
                var a = GameAssets.Get();
                var steel = a.Paint(new Color(0.3f, 0.32f, 0.34f), 0.8f, 0.5f);
                Part(PrimitiveType.Cylinder, t, new Vector3(0f, 1.2f, 0f), new Vector3(20f, 1.2f, 20f), steel);
                Part(PrimitiveType.Cylinder, t, new Vector3(0f, 9f, 0f), new Vector3(5f, 7f, 5f), a.Glow(teamGlow, 2.6f));
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * 45f;
                    Part(PrimitiveType.Cube, t, Quaternion.Euler(0f, ang, 0f) * new Vector3(0f, 8f, 6f), new Vector3(2.4f, 14f, 1.4f), mats[0], ang);
                }
                Part(PrimitiveType.Cylinder, t, new Vector3(0f, 16.4f, 0f), new Vector3(15f, 1f, 15f), steel);
            }
            return Finish(go, team, "POWER CORE", 4200f, 2.8f, new Vector3(0f, 20f, 0f), new Vector3(0f, 9f, 0f));
        }
    }
}
