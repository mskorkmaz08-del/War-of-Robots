using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public enum RobotType { Titan, Raptor, Arachne, Hornet, Juggernaut, Lancer, Bastion, Vanguard, Mammoth, Strider, Colossus, Scarab }

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
        static Material mPrimary, mSecondary, mDark, mMetal, mAccent, mGlow, mHot, mRubber, mBlack, mChrome;

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
            mChrome = A.Paint(new Color(0.78f, 0.79f, 0.8f), 1f, 0.95f, false);
        }

        /// <summary>
        /// How much bigger the robots are on the battlefield than the sculpted models
        /// (the hangar shows them at model size). With it the Titan stands about 21 m,
        /// three times the height of the houses and trees around it.
        /// </summary>
        public const float BattleScale = 1.4f;

        public static GameObject Build(RobotType type, Vector3 groundPos, Quaternion rotation, int team, Livery livery, bool player, string name, Loadout? loadout = null,
            float scale = BattleScale, int weaponLevel = -1)
        {
            PrepareMaterials(livery);
            segDims.Clear();
            footDims.Clear();
            // Sculpted models first; the procedural builders remain as a fallback.
            Chassis c = TryModel(type, groundPos, rotation, team, player, name, scale);
            if (c == null)
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
            var lo = loadout ?? Loadout.Default(type);
            FitWeapons(c, lo, team, player);
            var go = c.root;
            // Same rules for everyone: weapon damage and reload come from one table.
            // The player's own robot uses their upgrade levels; AI robots use level 0
            // unless the mode says otherwise.
            foreach (var w in go.GetComponents<Weapon>())
            {
                var id = w.slot == Weapon.Slot.Primary ? lo.primary : lo.special;
                int level = weaponLevel >= 0 ? weaponLevel : (player && team == 0 ? Upgrades.Level(id) : 0);
                WeaponStats.Apply(w, id, level);
            }
            if (!player) AimLikeAPerson(go);
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
            root.AddComponent<HitReaction>();
            ResetHealth(dmg);
            return dmg;
        }

        static void MergeLegs(List<WalkerLocomotion.Leg> legs)
        {
            AddLegRams(legs);
            foreach (var leg in legs)
            {
                MeshMerge.Merge(leg.thigh);
                MeshMerge.Merge(leg.knee);
                MeshMerge.Merge(leg.shin);
                MeshMerge.Merge(leg.foot);
            }
        }

        /// <summary>
        /// AI robots have the same armour, damage and reload as a player in the same
        /// machine. They only aim like a person would: a little scatter, no snap aim.
        /// </summary>
        static void AimLikeAPerson(GameObject go)
        {
            foreach (var w in go.GetComponents<Weapon>())
            {
                w.spread = 1.6f;
                w.aimAssist *= 0.6f;
            }
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
            float minDim = Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            if (shape == Shape.Box && minDim >= 0.45f)
            {
                // Big hull blocks become cast, rounded shells rather than sharp boxes.
                mesh = Shapes.RoundedBox(new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)), Mathf.Min(minDim * 0.2f, 0.45f));
                go.transform.localScale = Vector3.one;
            }
            else if (shape == Shape.Box && minDim >= 0.08f)
            {
                // Armour plates get real chamfered edges sized in metres.
                mesh = Shapes.BevelBox(new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                go.transform.localScale = Vector3.one;
            }
            else
            {
                // Bolts, rivets and thin rods: a six-sided prism reads the same at a fraction of the vertices.
                if (shape == Shape.Cylinder && Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) < 0.3f) shape = Shape.Hex;
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
