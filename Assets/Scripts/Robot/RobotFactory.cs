using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public enum RobotType { Titan, Raptor }

    /// <summary>Paint scheme for a robot.</summary>
    public struct Livery
    {
        public Color primary;
        public Color secondary;
        public Color accent;
        public Color glow;

        public static Livery Player => new Livery
        {
            primary = new Color(0.16f, 0.36f, 0.72f),
            secondary = new Color(0.62f, 0.64f, 0.68f),
            accent = new Color(1f, 0.55f, 0.08f),
            glow = new Color(0.2f, 0.85f, 1f)
        };

        public static Livery Enemy => new Livery
        {
            primary = new Color(0.62f, 0.1f, 0.08f),
            secondary = new Color(0.2f, 0.2f, 0.22f),
            accent = new Color(0.95f, 0.8f, 0.1f),
            glow = new Color(1f, 0.15f, 0.1f)
        };

        public static Livery Enemy2 => new Livery
        {
            primary = new Color(0.24f, 0.28f, 0.16f),
            secondary = new Color(0.12f, 0.12f, 0.12f),
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
    public static class RobotFactory
    {
        static GameAssets A => GameAssets.Get();

        // Shared materials for the robot being built.
        static Material mPrimary, mSecondary, mDark, mMetal, mAccent, mGlow, mHot, mRubber, mBlack;

        static void PrepareMaterials(Livery l)
        {
            mPrimary = A.Paint(l.primary, 0.55f, 0.5f);
            mSecondary = A.Paint(l.secondary, 0.75f, 0.55f);
            mDark = A.Paint(new Color(0.14f, 0.14f, 0.15f), 0.8f, 0.45f);
            mMetal = A.Paint(new Color(0.55f, 0.56f, 0.58f), 0.95f, 0.7f, false);
            mAccent = A.Paint(l.accent, 0.3f, 0.5f);
            mGlow = A.Glow(l.glow, 3f);
            mHot = A.Glow(new Color(1f, 0.45f, 0.1f), 2.5f);
            mRubber = A.Paint(new Color(0.06f, 0.06f, 0.06f), 0f, 0.25f, false);
            mBlack = A.Paint(new Color(0.03f, 0.03f, 0.035f), 0.5f, 0.3f, false);
        }

        public static GameObject Build(RobotType type, Vector3 groundPos, Quaternion rotation, int team, Livery livery, bool player, string name)
        {
            PrepareMaterials(livery);
            var go = type == RobotType.Titan
                ? BuildTitan(groundPos, rotation, team, player, name)
                : BuildRaptor(groundPos, rotation, team, player, name);
            if (!player) MakeEnemyFair(go);
            return go;
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
        // TITAN: giant two-legged walker with arm cannons and missile pods
        // =====================================================================

        static GameObject BuildTitan(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            const float stand = 8.5f;
            const float thigh = 5.0f;
            const float shin = 5.2f;

            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(groundPos + Vector3.up * stand, rotation);
            var t = root.transform;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 50000f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            AddBox(t, new Vector3(0f, 3.4f, 0f), new Vector3(7f, 7.4f, 5.4f));
            AddBox(t, new Vector3(0f, -3.8f, 0f), new Vector3(5f, 5.4f, 3.4f));

            // ---- Pelvis ----
            var hull = Group(t, "HullStatic", Vector3.zero);
            P(hull, Shape.Box, new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(3.6f, 1.6f, 3f), mSecondary);
            P(hull, Shape.Box, new Vector3(0f, -0.5f, 0.2f), Vector3.zero, new Vector3(2.6f, 1.2f, 2.6f), mDark);
            P(hull, Shape.Wedge, new Vector3(0f, -0.1f, 1.8f), new Vector3(0f, 180f, 0f), new Vector3(2.4f, 1.4f, 0.8f), mPrimary);
            P(hull, Shape.Box, new Vector3(0f, 0.35f, -1.6f), Vector3.zero, new Vector3(3f, 0.9f, 0.4f), mDark);
            for (int s = -1; s <= 1; s += 2)
            {
                P(hull, Shape.Sphere, new Vector3(s * 2.25f, -0.5f, 0f), Vector3.zero, Vector3.one * 1.9f, mDark);
                P(hull, Shape.Cylinder, new Vector3(s * 2.6f, -0.5f, 0f), new Vector3(0f, 0f, 90f), new Vector3(1.4f, 0.9f, 1.4f), mMetal);
                P(hull, Shape.Box, new Vector3(s * 2.1f, 0.6f, 0f), new Vector3(0f, 0f, -s * 18f), new Vector3(1.4f, 0.4f, 2.4f), mPrimary);
            }
            P(hull, Shape.Cylinder, new Vector3(0f, 0.9f, 0f), Vector3.zero, new Vector3(2.8f, 0.5f, 2.8f), mDark);

            // ---- Legs ----
            var legs = new List<WalkerLocomotion.Leg>();
            for (int s = -1; s <= 1; s += 2)
            {
                var hip = Group(t, s < 0 ? "HipL" : "HipR", new Vector3(s * 2.7f, -0.5f, 0f));

                var thighT = Group(t, "Thigh", Vector3.zero);
                P(thighT, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(1.5f, 1.7f, thigh * 0.92f), mSecondary);
                P(thighT, Shape.Box, new Vector3(0f, 0.95f, 0f), Vector3.zero, new Vector3(1.9f, 0.35f, thigh * 0.75f), mPrimary);
                P(thighT, Shape.Box, new Vector3(0f, 1.15f, -0.4f), Vector3.zero, new Vector3(0.5f, 0.15f, thigh * 0.4f), mAccent);
                P(thighT, Shape.Cylinder, new Vector3(s * 0.95f, -0.4f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.45f, thigh * 0.8f, 0.45f), mMetal);
                P(thighT, Shape.Cylinder, new Vector3(s * 0.95f, -0.4f, -thigh * 0.25f), new Vector3(90f, 0f, 0f), new Vector3(0.7f, thigh * 0.35f, 0.7f), mDark);
                P(thighT, Shape.Box, new Vector3(-s * 0.85f, 0f, 0f), Vector3.zero, new Vector3(0.3f, 1.2f, thigh * 0.6f), mDark);

                var kneeT = Group(t, "Knee", Vector3.zero);
                P(kneeT, Shape.Sphere, Vector3.zero, Vector3.zero, Vector3.one * 1.9f, mDark);
                P(kneeT, Shape.Wedge, new Vector3(0f, 0.5f, 1.0f), new Vector3(-20f, 180f, 0f), new Vector3(1.9f, 1.6f, 1.1f), mPrimary);
                P(kneeT, Shape.Box, new Vector3(0f, 0.9f, 1.25f), new Vector3(-20f, 0f, 0f), new Vector3(0.6f, 0.2f, 0.4f), mGlow);

                var shinT = Group(t, "Shin", Vector3.zero);
                P(shinT, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(1.4f, 1.5f, shin * 0.92f), mSecondary);
                P(shinT, Shape.Box, new Vector3(0f, 0.95f, 0.2f), Vector3.zero, new Vector3(1.9f, 0.45f, shin * 0.8f), mPrimary);
                P(shinT, Shape.Box, new Vector3(0f, 1.2f, 0.2f), Vector3.zero, new Vector3(1.1f, 0.12f, shin * 0.5f), mDark);
                P(shinT, Shape.Box, new Vector3(0f, 1.25f, shin * 0.25f), Vector3.zero, new Vector3(1.0f, 0.1f, 0.25f), mAccent);
                P(shinT, Shape.Box, new Vector3(0f, 1.25f, -shin * 0.1f), Vector3.zero, new Vector3(1.0f, 0.1f, 0.25f), mAccent);
                P(shinT, Shape.Cylinder, new Vector3(0f, -0.95f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.5f, shin * 0.85f, 0.5f), mMetal);
                P(shinT, Shape.Cylinder, new Vector3(0f, -0.95f, -shin * 0.2f), new Vector3(90f, 0f, 0f), new Vector3(0.75f, shin * 0.4f, 0.75f), mDark);
                P(shinT, Shape.Box, new Vector3(s * 0.8f, 0f, 0.3f), Vector3.zero, new Vector3(0.3f, 1.0f, shin * 0.5f), mDark);

                var footT = Group(t, "Foot", Vector3.zero);
                P(footT, Shape.Box, new Vector3(0f, 0f, 0.5f), Vector3.zero, new Vector3(2.4f, 0.7f, 3.8f), mDark);
                P(footT, Shape.Box, new Vector3(0f, 0.55f, 0.2f), Vector3.zero, new Vector3(1.8f, 0.6f, 2.4f), mSecondary);
                P(footT, Shape.Wedge, new Vector3(-0.65f, 0f, 2.9f), Vector3.zero, new Vector3(1.0f, 0.7f, 1.2f), mPrimary);
                P(footT, Shape.Wedge, new Vector3(0.65f, 0f, 2.9f), Vector3.zero, new Vector3(1.0f, 0.7f, 1.2f), mPrimary);
                P(footT, Shape.Wedge, new Vector3(0f, 0f, -1.9f), new Vector3(0f, 180f, 0f), new Vector3(1.4f, 0.7f, 1.0f), mPrimary);
                P(footT, Shape.Sphere, new Vector3(0f, 0.8f, 0f), Vector3.zero, Vector3.one * 1.3f, mMetal);
                P(footT, Shape.Box, new Vector3(0f, 0.2f, 2.45f), Vector3.zero, new Vector3(2.2f, 0.15f, 0.2f), mAccent);

                legs.Add(new WalkerLocomotion.Leg
                {
                    hip = hip, thigh = thighT, knee = kneeT, shin = shinT, foot = footT, side = s
                });
            }

            // ---- Upper body (turns to aim) ----
            var upper = Group(t, "Upper", new Vector3(0f, 1.4f, 0f));
            var torso = Group(upper, "TorsoStatic", Vector3.zero);
            P(torso, Shape.Cylinder, new Vector3(0f, 0.2f, 0f), Vector3.zero, new Vector3(3.2f, 0.6f, 3.2f), mDark);
            P(torso, Shape.Box, new Vector3(0f, 2.8f, -0.2f), Vector3.zero, new Vector3(5.6f, 3.6f, 4.2f), mSecondary);
            P(torso, Shape.Box, new Vector3(0f, 1.3f, 0f), Vector3.zero, new Vector3(3.8f, 1.4f, 3.2f), mDark);
            P(torso, Shape.Wedge, new Vector3(0f, 4.9f, 0.4f), new Vector3(0f, 180f, 0f), new Vector3(5.2f, 0.8f, 2.6f), mPrimary);
            P(torso, Shape.Box, new Vector3(0f, 2.9f, 2.05f), Vector3.zero, new Vector3(1.4f, 2.6f, 0.4f), mPrimary);
            P(torso, Shape.Box, new Vector3(0f, 3.6f, 2.3f), Vector3.zero, new Vector3(1.0f, 0.18f, 0.12f), mGlow);
            P(torso, Shape.Box, new Vector3(0f, 3.2f, 2.3f), Vector3.zero, new Vector3(1.0f, 0.18f, 0.12f), mGlow);
            P(torso, Shape.Box, new Vector3(0f, 2.8f, 2.3f), Vector3.zero, new Vector3(1.0f, 0.18f, 0.12f), mGlow);
            // Back reactor and vents
            P(torso, Shape.Cylinder, new Vector3(0f, 2.9f, -2.5f), new Vector3(90f, 0f, 0f), new Vector3(2.4f, 0.8f, 2.4f), mDark);
            P(torso, Shape.Cylinder, new Vector3(0f, 2.9f, -2.95f), new Vector3(90f, 0f, 0f), new Vector3(1.6f, 0.2f, 1.6f), mGlow);
            for (int i = 0; i < 4; i++)
                P(torso, Shape.Box, new Vector3(0f, 1.6f + i * 0.35f, -2.35f), Vector3.zero, new Vector3(3.6f, 0.12f, 0.3f), mBlack);
            // Exhaust stacks
            for (int s = -1; s <= 1; s += 2)
            {
                P(torso, Shape.Cylinder, new Vector3(s * 1.9f, 5.4f, -1.7f), Vector3.zero, new Vector3(0.7f, 2.2f, 0.7f), mDark);
                P(torso, Shape.Cylinder, new Vector3(s * 1.9f, 6.55f, -1.7f), Vector3.zero, new Vector3(0.55f, 0.1f, 0.55f), mHot);
                P(torso, Shape.Box, new Vector3(s * 2.2f, 1.1f, -2.0f), new Vector3(30f, 0f, 0f), new Vector3(1.3f, 1.2f, 1.4f), mDark);
            }
            // Cockpit head
            P(torso, Shape.Box, new Vector3(0f, 5.4f, 1.2f), Vector3.zero, new Vector3(2.4f, 1.4f, 2.2f), mSecondary);
            P(torso, Shape.Wedge, new Vector3(0f, 6.25f, 1.3f), new Vector3(0f, 180f, 0f), new Vector3(2.4f, 0.4f, 2.0f), mPrimary);
            P(torso, Shape.Box, new Vector3(0f, 5.5f, 2.32f), Vector3.zero, new Vector3(2.0f, 0.38f, 0.1f), mGlow);
            P(torso, Shape.Box, new Vector3(0f, 5.0f, 2.32f), Vector3.zero, new Vector3(1.6f, 0.3f, 0.1f), mBlack);
            for (int s = -1; s <= 1; s += 2)
                P(torso, Shape.Cylinder, new Vector3(s * 1.35f, 5.5f, 1.2f), new Vector3(0f, 0f, 90f), new Vector3(0.7f, 0.3f, 0.7f), mMetal);

            var antenna = Group(upper, "Antenna", new Vector3(-0.8f, 6.4f, 0.6f));
            P(antenna, Shape.Cylinder, new Vector3(0f, 1.2f, 0f), Vector3.zero, new Vector3(0.12f, 2.4f, 0.12f), mMetal);
            P(antenna, Shape.Sphere, new Vector3(0f, 2.45f, 0f), Vector3.zero, Vector3.one * 0.3f, mGlow);
            P(antenna, Shape.Box, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(0.5f, 0.3f, 0.5f), mDark);

            // Chest armour plates (blow off when damaged)
            var chestL = Group(upper, "ChestPlateL", new Vector3(-1.75f, 3.0f, 2.05f));
            chestL.localRotation = Quaternion.Euler(-8f, 14f, 0f);
            P(chestL, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(2.1f, 3.0f, 0.5f), mPrimary);
            P(chestL, Shape.Box, new Vector3(0f, 1.2f, 0.27f), Vector3.zero, new Vector3(1.9f, 0.18f, 0.05f), mAccent);
            var chestR = Group(upper, "ChestPlateR", new Vector3(1.75f, 3.0f, 2.05f));
            chestR.localRotation = Quaternion.Euler(-8f, -14f, 0f);
            P(chestR, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(2.1f, 3.0f, 0.5f), mPrimary);
            P(chestR, Shape.Box, new Vector3(0f, 1.2f, 0.27f), Vector3.zero, new Vector3(1.9f, 0.18f, 0.05f), mAccent);

            // Shoulder armour
            var pauldrons = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                var pd = Group(upper, s < 0 ? "PauldronL" : "PauldronR", new Vector3(s * 3.9f, 4.2f, 0f));
                pd.localRotation = Quaternion.Euler(0f, 0f, -s * 12f);
                P(pd, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(2.6f, 1.6f, 3.4f), mPrimary);
                P(pd, Shape.Box, new Vector3(0f, -0.9f, 0f), Vector3.zero, new Vector3(2.8f, 0.5f, 3.6f), mDark);
                P(pd, Shape.Box, new Vector3(s * 0.2f, 0.82f, 0f), Vector3.zero, new Vector3(2.0f, 0.1f, 0.4f), mAccent);
                P(pd, Shape.Box, new Vector3(s * 1.32f, 0f, 0f), Vector3.zero, new Vector3(0.1f, 0.6f, 2.4f), mAccent);
                pauldrons[s < 0 ? 0 : 1] = pd;
            }

            // Missile pods on the shoulders
            var tubes = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var pod = Group(upper, s < 0 ? "PodL" : "PodR", new Vector3(s * 3.9f, 5.8f, -0.6f));
                var podStatic = Group(pod, "PodStatic", Vector3.zero);
                P(podStatic, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(2.2f, 1.4f, 2.6f), mSecondary);
                P(podStatic, Shape.Box, new Vector3(0f, 0.75f, 0f), Vector3.zero, new Vector3(2.0f, 0.12f, 2.4f), mDark);
                P(podStatic, Shape.Box, new Vector3(s * 1.12f, 0f, 0f), Vector3.zero, new Vector3(0.05f, 0.4f, 2.0f), mAccent);
                for (int row = 0; row < 2; row++)
                for (int col = 0; col < 3; col++)
                {
                    Vector3 tp = new Vector3((col - 1) * 0.6f, 0.78f, 0.6f - row * 0.8f);
                    P(podStatic, Shape.Cylinder, tp, Vector3.zero, new Vector3(0.42f, 0.08f, 0.42f), mBlack);
                    P(podStatic, Shape.Sphere, tp + Vector3.up * 0.02f, Vector3.zero, new Vector3(0.28f, 0.1f, 0.28f), A.Glow(new Color(1f, 0.25f, 0.1f), 2f));
                    var tube = Group(pod, "Tube", tp + Vector3.up * 0.1f);
                    tube.localRotation = Quaternion.Euler(-70f, 0f, 0f);
                    tubes.Add(tube);
                }
                MeshMerge.Merge(podStatic);
            }

            // Arms with heavy cannons (pitch to aim)
            var arms = Group(upper, "Arms", new Vector3(0f, 3.6f, 0.2f));
            var armStatic = Group(arms, "ArmStatic", Vector3.zero);
            var cannonMuzzles = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                P(armStatic, Shape.Sphere, new Vector3(s * 3.9f, 0f, 0f), Vector3.zero, Vector3.one * 1.8f, mDark);
                P(armStatic, Shape.Box, new Vector3(s * 3.9f, -1.4f, 0f), Vector3.zero, new Vector3(1.3f, 2.6f, 1.3f), mSecondary);
                P(armStatic, Shape.Cylinder, new Vector3(s * 3.3f, -1.4f, -0.3f), Vector3.zero, new Vector3(0.35f, 2.4f, 0.35f), mMetal);
                P(armStatic, Shape.Sphere, new Vector3(s * 3.9f, -2.7f, 0.1f), Vector3.zero, Vector3.one * 1.3f, mDark);

                var barrel = Group(arms, s < 0 ? "CannonL" : "CannonR", new Vector3(s * 3.9f, -3.0f, 0.6f));
                var bs = Group(barrel, "CannonStatic", Vector3.zero);
                P(bs, Shape.Box, new Vector3(0f, 0f, 0.6f), Vector3.zero, new Vector3(1.8f, 1.8f, 3.4f), mPrimary);
                P(bs, Shape.Box, new Vector3(0f, 1.0f, 0.4f), Vector3.zero, new Vector3(1.2f, 0.25f, 2.6f), mDark);
                P(bs, Shape.Box, new Vector3(s * 0.95f, 0f, 0.8f), Vector3.zero, new Vector3(0.12f, 0.8f, 2.2f), mAccent);
                P(bs, Shape.Cylinder, new Vector3(0f, 0f, 4.2f), new Vector3(90f, 0f, 0f), new Vector3(0.95f, 5.2f, 0.95f), mDark);
                P(bs, Shape.Cylinder, new Vector3(0f, 0f, 3.0f), new Vector3(90f, 0f, 0f), new Vector3(1.25f, 0.35f, 1.25f), mGlow);
                P(bs, Shape.Cylinder, new Vector3(0f, 0f, 4.4f), new Vector3(90f, 0f, 0f), new Vector3(1.25f, 0.35f, 1.25f), mGlow);
                P(bs, Shape.Box, new Vector3(0f, 0f, 6.9f), Vector3.zero, new Vector3(1.6f, 1.1f, 1.0f), mMetal);
                P(bs, Shape.Box, new Vector3(0f, 0f, 6.9f), Vector3.zero, new Vector3(1.7f, 0.25f, 0.6f), mBlack);
                MeshMerge.Merge(bs);
                var muzzle = Group(barrel, "Muzzle", new Vector3(0f, 0f, 7.6f));
                cannonMuzzles.Add(muzzle);
            }

            // Jump jets on the back
            var jets = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var jet = Group(upper, "Jet", new Vector3(s * 2.2f, 0.6f, -2.6f));
                jet.localRotation = Quaternion.LookRotation(new Vector3(0f, -1f, -0.35f));
                jets.Add(jet);
            }

            var smoke = Group(upper, "SmokePoint", new Vector3(0.6f, 4.8f, -1.2f));

            MeshMerge.Merge(hull);
            MeshMerge.Merge(torso);
            MeshMerge.Merge(armStatic);
            foreach (var leg in legs)
            {
                MeshMerge.Merge(leg.thigh);
                MeshMerge.Merge(leg.knee);
                MeshMerge.Merge(leg.shin);
                MeshMerge.Merge(leg.foot);
            }

            // ---- Components ----
            root.AddComponent<RobotCommands>();
            var dmg = root.AddComponent<Damageable>();
            dmg.displayName = name;
            dmg.team = team;
            dmg.maxHealth = 2600f;
            dmg.size = 2.5f;
            dmg.smokePoint = smoke;
            dmg.detachableParts = new List<Transform> { pauldrons[0], chestL, antenna, pauldrons[1], chestR };
            ResetHealth(dmg);

            var walker = root.AddComponent<WalkerLocomotion>();
            walker.legs = legs.ToArray();
            walker.jetNozzles = jets.ToArray();
            walker.standHeight = stand;
            walker.thighLength = thigh;
            walker.shinLength = shin;
            walker.strideLength = 5.5f;
            walker.fuelDrain = 45f;

            var turret = root.AddComponent<Turret>();
            turret.yawPivot = upper;
            turret.pitchPivot = arms;
            turret.yawSpeed = 55f;
            turret.pitchSpeed = 30f;
            turret.maxYaw = 110f;
            turret.projectileSpeed = 110f;
            turret.compensateGravity = true;
            turret.acquireCone = player ? 75f : 180f;

            var cannon = root.AddComponent<HeavyCannon>();
            cannon.displayName = "Heavy Cannons";
            cannon.slot = Weapon.Slot.Primary;
            cannon.muzzles = cannonMuzzles.ToArray();
            cannon.cooldown = player ? 0.7f : 1.1f;
            cannon.color = new Color(1f, 0.6f, 0.2f);

            var missiles = root.AddComponent<MissileSwarm>();
            missiles.displayName = "Missile Swarm";
            missiles.slot = Weapon.Slot.Special;
            missiles.muzzles = tubes.ToArray();
            missiles.cooldown = 7f;
            missiles.missilesPerSalvo = 12;

            return root;
        }

        // =====================================================================
        // RAPTOR: armoured six-wheeler with ultra thrust and a plasma railgun
        // =====================================================================

        static GameObject BuildRaptor(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(groundPos + Vector3.up * 2.6f, rotation);
            var t = root.transform;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 30000f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            AddBox(t, new Vector3(0f, 1.0f, 0.2f), new Vector3(5.4f, 2.4f, 10.4f));
            AddBox(t, new Vector3(0f, 2.9f, -0.6f), new Vector3(3.6f, 1.8f, 4f));

            // ---- Hull ----
            var hull = Group(t, "HullStatic", Vector3.zero);
            P(hull, Shape.Box, new Vector3(0f, 1.0f, 0f), Vector3.zero, new Vector3(4.6f, 1.6f, 8.6f), mPrimary);
            P(hull, Shape.Box, new Vector3(0f, 0.2f, 0f), Vector3.zero, new Vector3(3.6f, 0.9f, 8.0f), mDark);
            P(hull, Shape.Wedge, new Vector3(0f, 1.0f, 5.0f), Vector3.zero, new Vector3(4.8f, 1.6f, 1.8f), mSecondary);
            P(hull, Shape.Box, new Vector3(0f, 0.2f, 5.55f), new Vector3(-25f, 0f, 0f), new Vector3(5.2f, 1.2f, 0.4f), mDark);
            for (int i = -1; i <= 1; i++)
            {
                P(hull, Shape.Cylinder, new Vector3(i * 1.6f, 0.4f, 6.1f), new Vector3(90f, 0f, 0f), new Vector3(0.45f, 1.4f, 0.45f), mMetal);
                P(hull, Shape.Sphere, new Vector3(i * 1.6f, 0.4f, 6.8f), Vector3.zero, Vector3.one * 0.45f, mMetal);
            }
            // Headlights
            for (int s = -1; s <= 1; s += 2)
            {
                P(hull, Shape.Box, new Vector3(s * 1.9f, 1.4f, 4.35f), Vector3.zero, new Vector3(0.6f, 0.3f, 0.15f), A.Glow(Color.white, 3f));
                P(hull, Shape.Box, new Vector3(s * 1.2f, 1.4f, 4.35f), Vector3.zero, new Vector3(0.4f, 0.3f, 0.15f), mGlow);
            }
            // Side armour strips and stripes
            for (int s = -1; s <= 1; s += 2)
            {
                P(hull, Shape.Box, new Vector3(s * 2.35f, 1.35f, 0f), Vector3.zero, new Vector3(0.2f, 0.35f, 7.6f), mAccent);
                P(hull, Shape.Box, new Vector3(s * 2.35f, 0.7f, 0f), Vector3.zero, new Vector3(0.25f, 0.6f, 6.8f), mDark);
                for (int k = 0; k < 4; k++)
                    P(hull, Shape.Box, new Vector3(s * 2.36f, 1.0f, -2.4f + k * 1.6f), Vector3.zero, new Vector3(0.15f, 0.9f, 0.18f), mBlack);
            }
            // Engine block and boosters
            P(hull, Shape.Box, new Vector3(0f, 1.6f, -4.3f), Vector3.zero, new Vector3(4.0f, 1.8f, 1.6f), mDark);
            P(hull, Shape.Box, new Vector3(0f, 2.6f, -4.0f), Vector3.zero, new Vector3(3.2f, 0.3f, 1.8f), mSecondary);
            for (int k = 0; k < 5; k++)
                P(hull, Shape.Box, new Vector3(0f, 2.8f, -4.7f + k * 0.35f), Vector3.zero, new Vector3(2.6f, 0.15f, 0.12f), mBlack);
            var nozzles = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                P(hull, Shape.Cylinder, new Vector3(s * 1.05f, 1.5f, -5.3f), new Vector3(90f, 0f, 0f), new Vector3(1.5f, 1.0f, 1.5f), mMetal);
                P(hull, Shape.Cylinder, new Vector3(s * 1.05f, 1.5f, -5.75f), new Vector3(90f, 0f, 0f), new Vector3(1.6f, 0.2f, 1.6f), mDark);
                P(hull, Shape.Cylinder, new Vector3(s * 1.05f, 1.5f, -5.82f), new Vector3(90f, 0f, 0f), new Vector3(1.1f, 0.06f, 1.1f), A.Glow(new Color(0.35f, 0.6f, 1f), 3f));
                var noz = Group(t, "Booster", new Vector3(s * 1.05f, 1.5f, -5.9f));
                noz.localRotation = Quaternion.LookRotation(Vector3.back);
                nozzles.Add(noz);
                P(hull, Shape.Cylinder, new Vector3(s * 1.7f, 3.1f, -3.5f), Vector3.zero, new Vector3(0.45f, 1.8f, 0.45f), mDark);
                P(hull, Shape.Cylinder, new Vector3(s * 1.7f, 4.02f, -3.5f), Vector3.zero, new Vector3(0.38f, 0.08f, 0.38f), mHot);
            }
            // Cockpit
            P(hull, Shape.Box, new Vector3(0f, 2.05f, 2.6f), Vector3.zero, new Vector3(2.4f, 0.9f, 2.4f), mSecondary);
            P(hull, Shape.Wedge, new Vector3(0f, 2.05f, 4.05f), Vector3.zero, new Vector3(2.4f, 0.9f, 0.7f), mSecondary);
            P(hull, Shape.Box, new Vector3(0f, 2.3f, 4.2f), new Vector3(-52f, 0f, 0f), new Vector3(2.0f, 0.5f, 0.06f), mGlow);

            // Fenders and skirts (blow off when damaged)
            var fenders = new List<Transform>();
            float[] wz = { 3.2f, 0f, -3.2f };
            for (int s = -1; s <= 1; s += 2)
            {
                foreach (float z in wz)
                {
                    var f = Group(t, "Fender", new Vector3(s * 3.3f, 2.25f, z));
                    P(f, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(1.6f, 0.25f, 2.8f), mPrimary);
                    P(f, Shape.Box, new Vector3(s * 0.75f, -0.35f, 0f), Vector3.zero, new Vector3(0.12f, 0.6f, 2.6f), mDark);
                    P(f, Shape.Box, new Vector3(0f, 0.15f, 1.2f), Vector3.zero, new Vector3(1.4f, 0.06f, 0.2f), mAccent);
                    MeshMerge.Merge(f);
                    fenders.Add(f);
                }
            }
            var roof = Group(t, "RoofPlate", new Vector3(0f, 2.6f, 2.5f));
            P(roof, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(2.6f, 0.2f, 2.0f), mPrimary);
            P(roof, Shape.Box, new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(0.3f, 0.05f, 1.8f), mAccent);

            // ---- Wheels ----
            var wheels = new List<WheeledLocomotion.Wheel>();
            for (int s = -1; s <= 1; s += 2)
            {
                foreach (float z in wz)
                {
                    var anchor = Group(t, "WheelAnchor", new Vector3(s * 3.3f, 0.4f, z));
                    var vis = Group(t, "Wheel", new Vector3(s * 3.3f, -0.6f, z));
                    var ws = Group(vis, "WheelStatic", Vector3.zero);
                    P(ws, Shape.Cylinder, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(2.8f, 1.2f, 2.8f), mRubber);
                    P(ws, Shape.Cylinder, new Vector3(s * 0.62f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(1.7f, 0.1f, 1.7f), mMetal);
                    P(ws, Shape.Cylinder, new Vector3(s * 0.7f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.7f, 0.12f, 0.7f), mAccent);
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k * 72f;
                        P(ws, Shape.Box, new Vector3(s * 0.66f, 0f, 0f), new Vector3(a, 0f, 0f), new Vector3(0.08f, 1.5f, 0.22f), mDark);
                    }
                    for (int k = 0; k < 10; k++)
                    {
                        float a = k * 36f * Mathf.Deg2Rad;
                        P(ws, Shape.Box, new Vector3(0f, Mathf.Cos(a) * 1.38f, Mathf.Sin(a) * 1.38f), new Vector3(k * 36f, 0f, 0f), new Vector3(1.22f, 0.12f, 0.35f), mRubber);
                    }
                    MeshMerge.Merge(ws);
                    wheels.Add(new WheeledLocomotion.Wheel { anchor = anchor, visual = vis });
                }
            }

            // ---- Turret ----
            var turretT = Group(t, "Turret", new Vector3(0f, 2.3f, -0.8f));
            var ts = Group(turretT, "TurretStatic", Vector3.zero);
            P(ts, Shape.Cylinder, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(3.4f, 0.4f, 3.4f), mDark);
            P(ts, Shape.Hex, new Vector3(0f, 0.9f, 0f), Vector3.zero, new Vector3(3.2f, 1.3f, 3.6f), mPrimary);
            P(ts, Shape.Box, new Vector3(0f, 1.62f, -0.3f), Vector3.zero, new Vector3(1.8f, 0.2f, 1.8f), mSecondary);
            P(ts, Shape.Cylinder, new Vector3(1.0f, 2.2f, -0.9f), Vector3.zero, new Vector3(0.1f, 1.4f, 0.1f), mMetal);
            P(ts, Shape.Sphere, new Vector3(1.0f, 2.95f, -0.9f), Vector3.zero, Vector3.one * 0.22f, mGlow);
            P(ts, Shape.Box, new Vector3(-1.62f, 0.9f, 0f), Vector3.zero, new Vector3(0.1f, 0.3f, 2.0f), mAccent);
            P(ts, Shape.Box, new Vector3(1.62f, 0.9f, 0f), Vector3.zero, new Vector3(0.1f, 0.3f, 2.0f), mAccent);

            var guns = Group(turretT, "Guns", new Vector3(0f, 1.0f, 1.2f));
            var gs = Group(guns, "GunStatic", Vector3.zero);
            var laserMuzzles = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                P(gs, Shape.Box, new Vector3(s * 1.25f, 0f, 0.6f), Vector3.zero, new Vector3(0.8f, 0.8f, 1.8f), mSecondary);
                P(gs, Shape.Cylinder, new Vector3(s * 1.25f, 0f, 2.6f), new Vector3(90f, 0f, 0f), new Vector3(0.4f, 2.6f, 0.4f), mDark);
                P(gs, Shape.Cylinder, new Vector3(s * 1.25f, 0f, 2.0f), new Vector3(90f, 0f, 0f), new Vector3(0.6f, 0.15f, 0.6f), mGlow);
                P(gs, Shape.Cylinder, new Vector3(s * 1.25f, 0f, 3.0f), new Vector3(90f, 0f, 0f), new Vector3(0.6f, 0.15f, 0.6f), mGlow);
                P(gs, Shape.Cylinder, new Vector3(s * 1.25f, 0f, 3.95f), new Vector3(90f, 0f, 0f), new Vector3(0.55f, 0.2f, 0.55f), mMetal);
                laserMuzzles.Add(Group(guns, "LaserMuzzle", new Vector3(s * 1.25f, 0f, 4.2f)));
            }
            // Railgun spine
            for (int s = -1; s <= 1; s += 2)
                P(gs, Shape.Box, new Vector3(s * 0.38f, 0.55f, 3.0f), Vector3.zero, new Vector3(0.22f, 0.55f, 6.4f), mDark);
            P(gs, Shape.Box, new Vector3(0f, 0.25f, 0.4f), Vector3.zero, new Vector3(1.2f, 0.9f, 1.6f), mPrimary);
            for (int k = 0; k < 5; k++)
                P(gs, Shape.Cylinder, new Vector3(0f, 0.55f, 1.3f + k * 1.1f), new Vector3(90f, 0f, 0f), new Vector3(1.25f, 0.14f, 1.25f), mGlow);
            var chargeGlow = Group(guns, "ChargeGlow", new Vector3(0f, 0.55f, 6.4f));
            P(chargeGlow, Shape.Sphere, Vector3.zero, Vector3.zero, Vector3.one, A.Glow(new Color(0.4f, 0.8f, 1f), 4f));
            chargeGlow.localScale = Vector3.one * 0.2f;
            var railMuzzle = Group(guns, "RailMuzzle", new Vector3(0f, 0.55f, 6.6f));

            var smoke = Group(t, "SmokePoint", new Vector3(0f, 2.4f, -3.2f));

            MeshMerge.Merge(hull);
            MeshMerge.Merge(ts);
            MeshMerge.Merge(gs);

            // ---- Components ----
            root.AddComponent<RobotCommands>();
            var dmg = root.AddComponent<Damageable>();
            dmg.displayName = name;
            dmg.team = team;
            dmg.maxHealth = 2000f;
            dmg.size = 2f;
            dmg.smokePoint = smoke;
            dmg.detachableParts = new List<Transform> { fenders[0], roof, fenders[5], fenders[1], fenders[3] };
            dmg.frontArmor = 0.3f;
            ResetHealth(dmg);

            var drive = root.AddComponent<WheeledLocomotion>();
            drive.wheels = wheels.ToArray();
            drive.thrusterNozzles = nozzles.ToArray();
            drive.wheelRadius = 1.4f;
            drive.suspensionTravel = 1.1f;

            var turret = root.AddComponent<Turret>();
            turret.yawPivot = turretT;
            turret.pitchPivot = guns;
            turret.yawSpeed = 90f;
            turret.pitchSpeed = 45f;
            turret.maxYaw = 150f;
            turret.acquireCone = player ? 80f : 180f;

            var laser = root.AddComponent<PulseLaser>();
            laser.displayName = "Pulse Lasers";
            laser.slot = Weapon.Slot.Primary;
            laser.muzzles = laserMuzzles.ToArray();
            laser.cooldown = player ? 0.11f : 0.18f;
            laser.color = team == 0 ? new Color(0.3f, 0.85f, 1f) : new Color(1f, 0.25f, 0.2f);

            var rail = root.AddComponent<PlasmaRailgun>();
            rail.displayName = "Plasma Railgun";
            rail.slot = Weapon.Slot.Special;
            rail.muzzles = new[] { railMuzzle };
            rail.cooldown = 7f;
            rail.chargeGlow = chargeGlow;
            rail.color = new Color(0.45f, 0.8f, 1f);

            return root;
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
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Shapes.Get(shape);
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
