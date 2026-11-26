using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public enum RobotType { Titan, Raptor, Arachne, Hornet, Juggernaut, Lancer, Bastion, Vanguard, Mammoth }

    /// <summary>Paint scheme for a robot.</summary>
    public struct Livery
    {
        public Color primary;
        public Color secondary;
        public Color accent;
        public Color glow;

        public static Livery Player => new Livery
        {
            primary = new Color(0.22f, 0.3f, 0.4f),
            secondary = new Color(0.3f, 0.31f, 0.33f),
            accent = new Color(0.82f, 0.56f, 0.12f),
            glow = new Color(0.35f, 0.8f, 1f)
        };

        public static Livery Enemy => new Livery
        {
            primary = new Color(0.4f, 0.12f, 0.08f),
            secondary = new Color(0.22f, 0.21f, 0.21f),
            accent = new Color(0.8f, 0.62f, 0.12f),
            glow = new Color(1f, 0.15f, 0.1f)
        };

        public static Livery Ally => new Livery
        {
            primary = new Color(0.24f, 0.33f, 0.32f),
            secondary = new Color(0.3f, 0.31f, 0.31f),
            accent = new Color(0.8f, 0.62f, 0.18f),
            glow = new Color(0.3f, 1f, 0.85f)
        };

        public static Livery Enemy2 => new Livery
        {
            primary = new Color(0.27f, 0.29f, 0.18f),
            secondary = new Color(0.16f, 0.16f, 0.15f),
            accent = new Color(0.9f, 0.35f, 0.05f),
            glow = new Color(1f, 0.4f, 0.05f)
        };
    }

    /// <summary>
    /// Builds the giant robots from hundreds of shaped parts at runtime, then merges
    /// the parts into a few meshes for performance. Sizes are in metres:
    /// the Titan walker stands about 15 m tall, the Raptor is a 10 m long armoured
    /// six-wheeler.
    /// </summary>
    public static partial class RobotFactory
    {
        static GameAssets A => GameAssets.Get();

        // Shared materials for the robot being built.
        static Material mPrimary, mSecondary, mDark, mMetal, mAccent, mGlow, mHot, mRubber, mBlack;

        static void PrepareMaterials(Livery l)
        {
            // Matte, worn military paint: gloss makes big machines look like plastic toys.
            mPrimary = A.Paint(l.primary, 0.45f, 0.22f);
            mSecondary = A.Paint(l.secondary, 0.65f, 0.28f);
            mDark = A.Paint(new Color(0.11f, 0.11f, 0.12f), 0.75f, 0.3f);
            mMetal = A.Paint(new Color(0.42f, 0.42f, 0.43f), 0.9f, 0.45f, false);
            mAccent = A.Paint(l.accent, 0.3f, 0.25f);
            mGlow = A.Glow(l.glow, 2f);
            mHot = A.Glow(new Color(1f, 0.45f, 0.1f), 2.5f);
            mRubber = A.Paint(new Color(0.06f, 0.06f, 0.06f), 0f, 0.25f, false);
            mBlack = A.Paint(new Color(0.03f, 0.03f, 0.035f), 0.5f, 0.3f, false);
        }

        public static GameObject Build(RobotType type, Vector3 groundPos, Quaternion rotation, int team, Livery livery, bool player, string name, Loadout? loadout = null)
        {
            PrepareMaterials(livery);
            Chassis c;
            switch (type)
            {
                case RobotType.Raptor: c = BuildRaptor(groundPos, rotation, team, player, name); break;
                case RobotType.Arachne: c = BuildArachne(groundPos, rotation, team, player, name); break;
                case RobotType.Hornet: c = BuildHornet(groundPos, rotation, team, player, name); break;
                case RobotType.Juggernaut: c = BuildJuggernaut(groundPos, rotation, team, player, name); break;
                case RobotType.Lancer: c = BuildLancer(groundPos, rotation, team, player, name); break;
                case RobotType.Bastion: c = BuildBastion(groundPos, rotation, team, player, name); break;
                case RobotType.Vanguard: c = BuildVanguard(groundPos, rotation, team, player, name); break;
                case RobotType.Mammoth: c = BuildMammoth(groundPos, rotation, team, player, name); break;
                default: c = BuildTitan(groundPos, rotation, team, player, name); break;
            }
            FitWeapons(c, loadout ?? Loadout.Default(type), team, player);
            var go = c.root;
            if (!player) MakeEnemyFair(go);
            return go;
        }

        /// <summary>Shared rigidbody setup for every robot.</summary>
        static GameObject NewRoot(string name, Vector3 pos, Quaternion rotation, float mass, bool fast)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(pos, rotation);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = fast ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Continuous;
            return root;
        }

        static Damageable AddDamageable(GameObject root, string name, int team, float health, float size, Transform smoke, List<Transform> parts, float frontArmor = 0f)
        {
            root.AddComponent<RobotCommands>();
            var dmg = root.AddComponent<Damageable>();
            dmg.displayName = name;
            dmg.team = team;
            dmg.maxHealth = health;
            dmg.size = size;
            dmg.smokePoint = smoke;
            dmg.detachableParts = parts;
            if (frontArmor > 0f) dmg.frontArmor = frontArmor;
            ResetHealth(dmg);
            return dmg;
        }

        static void MergeLegs(List<WalkerLocomotion.Leg> legs)
        {
            foreach (var leg in legs)
            {
                MeshMerge.Merge(leg.thigh);
                MeshMerge.Merge(leg.knee);
                MeshMerge.Merge(leg.shin);
                MeshMerge.Merge(leg.foot);
            }
        }

        /// <summary>Enemies are tougher-looking than they are: less armour, slower and less accurate weapons.</summary>
        static void MakeEnemyFair(GameObject go)
        {
            var d = go.GetComponent<Damageable>();
            if (d != null)
            {
                d.maxHealth *= 0.7f;
                ResetHealth(d);
            }
            foreach (var w in go.GetComponents<Weapon>())
            {
                w.cooldown *= 1.4f;
                w.spread = 2.5f;
                w.aimAssist *= 0.5f;
            }
            var swarm = go.GetComponent<MissileSwarm>();
            if (swarm != null) swarm.missilesPerSalvo = 6;
            var rail = go.GetComponent<PlasmaRailgun>();
            if (rail != null) rail.chargeTime = 1.8f;
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        static void ResetHealth(Damageable d)
        {
            // maxHealth was set after Awake; restore full health.
            d.Heal(d.maxHealth);
        }

        static Transform Group(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        static Transform P(Transform parent, Shape shape, Vector3 localPos, Vector3 localEuler, Vector3 scale, Material mat)
        {
            var go = new GameObject(shape.ToString());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            Mesh mesh;
            if (shape == Shape.Box && Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z))) >= 0.08f)
            {
                // Armour plates get real chamfered edges sized in metres.
                mesh = Shapes.BevelBox(new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                go.transform.localScale = Vector3.one;
            }
            else
            {
                mesh = Shapes.Get(shape);
                go.transform.localScale = scale;
            }
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        static void AddBox(Transform t, Vector3 center, Vector3 size)
        {
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }
    }
}
