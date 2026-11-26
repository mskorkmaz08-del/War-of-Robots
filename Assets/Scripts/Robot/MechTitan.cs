using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public static partial class RobotFactory
    {
        // =====================================================================
        // TITAN: 15 m heavy assault walker. Barrel chest, cockpit head, heavy
        // armoured legs on hydraulic rams, arm weapons and shoulder hardpoints.
        // =====================================================================

        static Chassis BuildTitan(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            const float stand = 8.5f;
            const float thigh = 5.0f;
            const float shin = 5.2f;

            var root = NewRoot(name, groundPos + Vector3.up * stand, rotation, 50000f, false);
            var t = root.transform;
            AddBox(t, new Vector3(0f, 3.6f, 0f), new Vector3(7f, 7.6f, 5.4f));
            AddBox(t, new Vector3(0f, -3.8f, 0f), new Vector3(5f, 5.4f, 3.4f));

            // ---- Pelvis ----
            var hull = Group(t, "HullStatic", Vector3.zero);
            Shell(hull, Vector3.zero, Vector3.zero, new Vector3(3.8f, 1.9f, 3.2f), mDark, 0.5f, 0.9f, 0.9f);
            Shell(hull, new Vector3(0f, 0.6f, 0f), Vector3.zero, new Vector3(4.2f, 0.7f, 3.4f), mSecondary, 0.25f);
            Armor(hull, new Vector3(0f, -0.4f, 1.65f), new Vector3(14f, 0f, 0f), new Vector3(2.2f, 1.6f, 0.45f), mPrimary);
            Armor(hull, new Vector3(0f, -0.3f, -1.65f), new Vector3(-10f, 180f, 0f), new Vector3(2.6f, 1.4f, 0.4f), mPrimary);
            DiscPart(hull, new Vector3(0f, 1.05f, 0f), Vector3.zero, 3.2f, 0.5f, mDark);
            for (int s = -1; s <= 1; s += 2)
            {
                Joint(hull, new Vector3(s * 2.45f, -0.5f, 0f), 2.5f, 1.0f);
                Shell(hull, new Vector3(s * 2.2f, 0.45f, 0f), new Vector3(0f, 0f, -s * 22f), new Vector3(1.4f, 0.9f, 3.0f), mPrimary, 0.35f, 0.85f, 0.9f);
                Hose(hull, new[] { new Vector3(s * 1.4f, 0.3f, -1.4f), new Vector3(s * 2.1f, -0.2f, -1.6f), new Vector3(s * 2.5f, -1.2f, -0.9f) }, 0.12f);
            }

            // ---- Legs ----
            var legs = new List<WalkerLocomotion.Leg>();
            for (int s = -1; s <= 1; s += 2)
            {
                var hip = Group(t, s < 0 ? "HipL" : "HipR", new Vector3(s * 2.75f, -0.5f, 0f));

                var thighT = Group(t, "Thigh", Vector3.zero);
                LegSegment(thighT, thigh, 2.2f, 2.4f, s, false);

                var kneeT = Group(t, "Knee", Vector3.zero);
                Joint(kneeT, Vector3.zero, 2.3f, 2.1f);
                Shell(kneeT, new Vector3(0f, 0.35f, 1.15f), new Vector3(-18f, 0f, 0f), new Vector3(2.0f, 2.0f, 0.7f), mPrimary, 0.3f, 0.8f, 0.9f);
                P(kneeT, Shape.Box, new Vector3(0f, 0.9f, 1.5f), new Vector3(-18f, 0f, 0f), new Vector3(1.2f, 0.08f, 0.1f), mBlack);
                Lamp(kneeT, new Vector3(0f, 1.3f, 1.35f), new Vector3(-18f, 0f, 0f), 0.35f, mGlow);

                var shinT = Group(t, "Shin", Vector3.zero);
                LegSegment(shinT, shin, 2.1f, 2.3f, s, true);

                var footT = Group(t, "Foot", Vector3.zero);
                MechFoot(footT, 3.2f, 5.0f, mPrimary);

                legs.Add(new WalkerLocomotion.Leg { hip = hip, thigh = thighT, knee = kneeT, shin = shinT, foot = footT, side = s });
            }

            // ---- Upper body (turns to aim) ----
            var upper = Group(t, "Upper", new Vector3(0f, 1.4f, 0f));
            var torso = Group(upper, "TorsoStatic", Vector3.zero);
            // Waist bearing and the rams that brace the chest on it
            DiscPart(torso, new Vector3(0f, 0.25f, 0f), Vector3.zero, 3.4f, 0.6f, mDark);
            Shell(torso, new Vector3(0f, 1.3f, 0.1f), Vector3.zero, new Vector3(3.8f, 1.6f, 3.0f), mDark, 0.45f, 1.25f, 1.15f);
            for (int s = -1; s <= 1; s += 2)
                Piston(torso, new Vector3(s * 1.3f, 0.45f, 1.0f), new Vector3(s * 1.7f, 2.1f, 1.45f), 0.18f);
            // Barrel chest: one cast hull, a raised collar and a keel down the front
            Shell(torso, new Vector3(0f, 3.0f, -0.3f), Vector3.zero, new Vector3(5.8f, 3.6f, 4.4f), mSecondary, 1.0f, 0.9f, 0.88f);
            Shell(torso, new Vector3(0f, 4.6f, -0.2f), Vector3.zero, new Vector3(4.8f, 0.7f, 3.6f), mPrimary, 0.3f, 0.92f, 0.9f);
            Shell(torso, new Vector3(0f, 2.65f, 1.95f), new Vector3(-8f, 0f, 0f), new Vector3(1.3f, 2.9f, 0.7f), mPrimary, 0.28f, 0.75f, 1f);
            P(torso, Shape.Box, new Vector3(0f, 3.2f, 2.33f), new Vector3(-8f, 0f, 0f), new Vector3(0.7f, 0.08f, 0.1f), mBlack);
            P(torso, Shape.Box, new Vector3(0f, 2.3f, 2.38f), new Vector3(-8f, 0f, 0f), new Vector3(0.7f, 0.08f, 0.1f), mBlack);
            Vent(torso, new Vector3(0f, 1.45f, 1.62f), new Vector3(-10f, 0f, 0f), 1.4f, 0.8f, 4);
            // Back: reactor housing with a glowing core, vents and exhaust stacks
            Shell(torso, new Vector3(0f, 2.9f, -2.55f), Vector3.zero, new Vector3(3.8f, 3.0f, 1.3f), mPrimary, 0.5f, 0.9f, 0.9f);
            DiscPart(torso, new Vector3(0f, 2.9f, -3.2f), new Vector3(90f, 0f, 0f), 2.1f, 0.4f, mDark);
            P(torso, Shape.Cylinder, new Vector3(0f, 2.9f, -3.42f), new Vector3(90f, 0f, 0f), new Vector3(0.95f, 0.06f, 0.95f), mGlow);
            for (int s = -1; s <= 1; s += 2)
            {
                Vent(torso, new Vector3(s * 2.75f, 2.7f, -0.4f), new Vector3(0f, s * 90f, 0f), 2.2f, 1.5f, 5);
                P(torso, Shape.Cylinder, new Vector3(s * 1.6f, 5.35f, -2.0f), Vector3.zero, new Vector3(0.75f, 1.9f, 0.75f), mDark);
                DiscPart(torso, new Vector3(s * 1.6f, 4.75f, -2.0f), Vector3.zero, 1.0f, 0.25f, mSecondary);
                P(torso, Shape.Cylinder, new Vector3(s * 1.6f, 6.31f, -2.0f), Vector3.zero, new Vector3(0.58f, 0.06f, 0.58f), mHot);
                Shell(torso, new Vector3(s * 2.25f, 1.0f, -2.0f), new Vector3(30f, 0f, 0f), new Vector3(1.3f, 1.2f, 1.4f), mDark, 0.3f);
                // Hoses from the reactor to the shoulders
                Hose(torso, new[] { new Vector3(s * 1.5f, 3.9f, -2.6f), new Vector3(s * 2.6f, 4.5f, -1.9f), new Vector3(s * 3.3f, 4.1f, -0.9f) }, 0.16f);
                Hose(torso, new[] { new Vector3(s * 1.2f, 2.0f, -2.9f), new Vector3(s * 2.4f, 1.6f, -2.4f), new Vector3(s * 2.9f, 2.2f, -1.4f) }, 0.12f);
            }
            // Head, sunk between the shoulders on an armoured collar
            Shell(torso, new Vector3(0f, 4.95f, 0.75f), Vector3.zero, new Vector3(2.4f, 0.6f, 2.0f), mDark, 0.25f);
            Cockpit(torso, new Vector3(0f, 5.55f, 1.05f), 2.3f, 1.35f, 2.1f);
            var antenna = Antenna(upper, new Vector3(-1.0f, 6.0f, 0.2f), 2.2f);

            // Chest armour plates (blow off when damaged)
            var chestL = Group(upper, "ChestPlateL", new Vector3(-1.8f, 3.0f, 2.0f));
            chestL.localRotation = Quaternion.Euler(-6f, 16f, 0f);
            Shell(chestL, Vector3.zero, Vector3.zero, new Vector3(2.3f, 3.0f, 0.6f), mPrimary, 0.28f, 0.9f, 1f);
            P(chestL, Shape.Box, new Vector3(0f, -0.5f, 0.31f), Vector3.zero, new Vector3(1.7f, 0.06f, 0.08f), mBlack);
            P(chestL, Shape.Box, new Vector3(0.15f, 1.0f, 0.31f), Vector3.zero, new Vector3(1.3f, 0.16f, 0.05f), mAccent);
            Lamp(chestL, new Vector3(-0.6f, 0.3f, 0.35f), Vector3.zero, 0.3f, mGlow);
            var chestR = Group(upper, "ChestPlateR", new Vector3(1.8f, 3.0f, 2.0f));
            chestR.localRotation = Quaternion.Euler(-6f, -16f, 0f);
            Shell(chestR, Vector3.zero, Vector3.zero, new Vector3(2.3f, 3.0f, 0.6f), mPrimary, 0.28f, 0.9f, 1f);
            P(chestR, Shape.Box, new Vector3(0f, -0.5f, 0.31f), Vector3.zero, new Vector3(1.7f, 0.06f, 0.08f), mBlack);
            P(chestR, Shape.Box, new Vector3(-0.15f, 1.0f, 0.31f), Vector3.zero, new Vector3(1.3f, 0.16f, 0.05f), mAccent);
            Stencil(chestR, new Vector3(0.1f, -0.95f, 0.31f), Vector3.zero, 1.2f);

            // Shoulder armour: big cast pauldrons with a side skirt and a work light
            var pauldrons = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                var pd = Group(upper, s < 0 ? "PauldronL" : "PauldronR", new Vector3(s * 4.0f, 4.4f, 0.1f));
                pd.localRotation = Quaternion.Euler(0f, 0f, -s * 12f);
                Shell(pd, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(3.1f, 1.5f, 3.8f), mPrimary, 0.6f, 0.82f, 0.88f);
                Shell(pd, new Vector3(s * 1.45f, -0.6f, 0f), new Vector3(0f, 0f, s * 8f), new Vector3(0.5f, 1.9f, 3.4f), mPrimary, 0.2f, 1f, 0.92f);
                P(pd, Shape.Box, new Vector3(s * 1.72f, -0.3f, 0f), Vector3.zero, new Vector3(0.06f, 0.22f, 2.8f), mAccent);
                for (int k = 0; k < 3; k++)
                    P(pd, Shape.Box, new Vector3(s * 0.3f, 0.92f, -0.9f + k * 0.6f), Vector3.zero, new Vector3(1.6f, 0.06f, 0.12f), mBlack);
                Lamp(pd, new Vector3(0f, 0.1f, 1.92f), Vector3.zero, 0.45f, A.Glow(Color.white, 3f));
                pauldrons[s < 0 ? 0 : 1] = pd;
            }

            // ---- Arms (pitch to aim) ----
            var arms = Group(upper, "Arms", new Vector3(0f, 3.5f, 0.2f));
            var armStatic = Group(arms, "ArmStatic", Vector3.zero);
            var mounts = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 4.0f;
                Joint(armStatic, new Vector3(x, 0f, 0f), 2.4f, 1.7f);
                // Upper arm with its own ram
                Shell(armStatic, new Vector3(x, -1.4f, -0.1f), Vector3.zero, new Vector3(1.3f, 2.4f, 1.4f), mDark, 0.35f);
                Shell(armStatic, new Vector3(x + s * 0.65f, -1.3f, -0.1f), Vector3.zero, new Vector3(0.4f, 2.2f, 1.6f), mSecondary, 0.15f);
                Piston(armStatic, new Vector3(x - s * 0.1f, -0.5f, 0.75f), new Vector3(x - s * 0.1f, -2.3f, 0.85f), 0.2f);
                // Elbow and a heavy armoured forearm
                Joint(armStatic, new Vector3(x, -2.75f, 0.1f), 1.8f, 1.6f);
                Shell(armStatic, new Vector3(x, -2.75f, 1.0f), Vector3.zero, new Vector3(2.0f, 2.0f, 2.6f), mSecondary, 0.5f, 0.92f, 1f);
                Shell(armStatic, new Vector3(x, -1.72f, 1.0f), Vector3.zero, new Vector3(1.6f, 0.35f, 2.2f), mPrimary, 0.15f);
                Shell(armStatic, new Vector3(x + s * 1.02f, -2.75f, 1.0f), Vector3.zero, new Vector3(0.3f, 1.5f, 2.2f), mPrimary, 0.12f);
                P(armStatic, Shape.Box, new Vector3(x + s * 1.18f, -2.75f, 1.0f), Vector3.zero, new Vector3(0.05f, 0.2f, 1.6f), mAccent);
                Hose(armStatic, new[] { new Vector3(x - s * 0.6f, -0.6f, -0.6f), new Vector3(x - s * 0.8f, -1.9f, -0.5f), new Vector3(x - s * 0.7f, -2.8f, 0.2f), new Vector3(x - s * 0.6f, -2.9f, 0.8f) }, 0.12f);
                // Clamp that holds the weapon
                for (int c = -1; c <= 1; c += 2)
                    Shell(armStatic, new Vector3(x, -2.75f + c * 0.85f, 1.85f), Vector3.zero, new Vector3(1.3f, 0.3f, 0.7f), mDark, 0.1f);
                mounts[s < 0 ? 0 : 1] = Group(arms, s < 0 ? "MountL" : "MountR", new Vector3(x, -2.75f, 1.6f));
            }
            var shoulder = Group(arms, "ShoulderMount", new Vector3(-4.0f, 2.2f, -0.2f));
            var backs = new[]
            {
                Group(upper, "BackL", new Vector3(-4.0f, 5.8f, -0.7f)),
                Group(upper, "BackR", new Vector3(4.0f, 5.8f, -0.7f))
            };

            // Jump jets on the back
            var jets = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                P(torso, Shape.Cylinder, new Vector3(s * 2.2f, 0.9f, -2.4f), new Vector3(20f, 0f, 0f), new Vector3(1.1f, 0.9f, 1.1f), mMetal);
                var jet = Group(upper, "Jet", new Vector3(s * 2.2f, 0.4f, -2.6f));
                jet.localRotation = Quaternion.LookRotation(new Vector3(0f, -1f, -0.35f));
                jets.Add(jet);
            }

            var smoke = Group(upper, "SmokePoint", new Vector3(0.6f, 4.8f, -1.2f));

            // Crew-scale details: roof deck railing, access ladder, hatch, stencils
            Railing(torso, new Vector3(0f, 4.8f, -1.0f), 4.6f, 2.2f);
            Ladder(torso, new Vector3(1.7f, 1.0f, -2.6f), 3.8f, 180f);
            Hatch(torso, new Vector3(-0.6f, 4.82f, -0.9f), new Vector3(-90f, 0f, 0f), 1.0f);
            Stencil(torso, new Vector3(-1.2f, 4.0f, -2.58f), new Vector3(0f, 180f, 0f), 1.6f);
            MeshMerge.Merge(hull);
            MeshMerge.Merge(torso);
            MeshMerge.Merge(armStatic);
            foreach (var pd in pauldrons) MeshMerge.Merge(pd);
            MeshMerge.Merge(chestL);
            MeshMerge.Merge(chestR);
            MeshMerge.Merge(antenna);
            MergeLegs(legs);

            AddDamageable(root, name, team, 2600f, 2.5f, smoke, new List<Transform> { pauldrons[0], chestL, antenna, pauldrons[1], chestR });

            var walker = root.AddComponent<WalkerLocomotion>();
            walker.legs = legs.ToArray();
            walker.jetNozzles = jets.ToArray();
            walker.standHeight = stand;
            walker.thighLength = thigh;
            walker.shinLength = shin;
            walker.strideLength = 5.5f;
            walker.fuelDrain = 20f;
            walker.sprintBoost = true;

            var turret = root.AddComponent<Turret>();
            turret.yawPivot = upper;
            turret.pitchPivot = arms;
            turret.yawSpeed = 55f;
            turret.pitchSpeed = 30f;
            turret.maxYaw = 110f;
            turret.acquireCone = player ? 75f : 180f;

            return new Chassis { root = root, turret = turret, armMounts = mounts, shoulderMount = shoulder, backMounts = backs, armScale = 1f, shoulderScale = 1f, backScale = 1f };
        }
    }
}
