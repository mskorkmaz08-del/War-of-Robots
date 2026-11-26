using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public static partial class RobotFactory
    {
        // =====================================================================
        // VANGUARD: medium all-round walker. Broad V-shaped chest, twin-eyed
        // helmet head, a big armour shield on the left forearm and strong jump jets.
        // =====================================================================

        static Chassis BuildVanguard(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            const float stand = 7.8f;
            const float thigh = 4.6f;
            const float shin = 4.8f;

            var root = NewRoot(name, groundPos + Vector3.up * stand, rotation, 38000f, false);
            var t = root.transform;
            AddBox(t, new Vector3(0f, 2.9f, 0f), new Vector3(6.0f, 5.6f, 4.4f));
            AddBox(t, new Vector3(0f, -3.4f, 0f), new Vector3(4.0f, 5f, 3f));

            // ---- Pelvis ----
            var hull = Group(t, "HullStatic", Vector3.zero);
            P(hull, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(3.0f, 1.5f, 2.6f), mDark);
            Armor(hull, new Vector3(0f, -0.4f, 1.4f), new Vector3(16f, 0f, 0f), new Vector3(1.7f, 1.4f, 0.4f), mPrimary);
            P(hull, Shape.Cylinder, new Vector3(0f, 0.9f, 0f), Vector3.zero, new Vector3(2.6f, 0.45f, 2.6f), mDark);
            for (int s = -1; s <= 1; s += 2)
            {
                Joint(hull, new Vector3(s * 2.0f, -0.35f, 0f), 1.8f, 0.8f);
                // Hanging tassets
                Armor(hull, new Vector3(s * 1.7f, -0.6f, 0.9f), new Vector3(8f, s * 20f, 0f), new Vector3(1.3f, 1.6f, 0.3f), mPrimary);
            }

            // ---- Legs ----
            var legs = new List<WalkerLocomotion.Leg>();
            for (int s = -1; s <= 1; s += 2)
            {
                var hip = Group(t, s < 0 ? "HipL" : "HipR", new Vector3(s * 2.35f, -0.35f, 0f));

                var thighT = Group(t, "Thigh", Vector3.zero);
                LegSegment(thighT, thigh, 1.55f, 1.75f, s, false);

                var kneeT = Group(t, "Knee", Vector3.zero);
                Joint(kneeT, Vector3.zero, 1.65f, 1.5f);
                Armor(kneeT, new Vector3(0f, 0.5f, 0.85f), new Vector3(-30f, 0f, 0f), new Vector3(1.6f, 1.8f, 0.45f), mPrimary);
                P(kneeT, Shape.Wedge, new Vector3(0f, 1.45f, 0.95f), new Vector3(-30f, 0f, 0f), new Vector3(0.7f, 0.5f, 0.4f), mAccent);

                var shinT = Group(t, "Shin", Vector3.zero);
                LegSegment(shinT, shin, 1.5f, 1.7f, s, true);
                // Ankle thruster
                P(shinT, Shape.Cylinder, new Vector3(0f, -1.0f, 1.5f), new Vector3(90f, 0f, 0f), new Vector3(0.7f, 0.6f, 0.7f), mMetal);

                var footT = Group(t, "Foot", Vector3.zero);
                MechFoot(footT, 2.6f, 4.3f, mSecondary);

                legs.Add(new WalkerLocomotion.Leg { hip = hip, thigh = thighT, knee = kneeT, shin = shinT, foot = footT, side = s });
            }

            // ---- Upper body ----
            var upper = Group(t, "Upper", new Vector3(0f, 1.2f, 0f));
            var torso = Group(upper, "TorsoStatic", Vector3.zero);
            P(torso, Shape.Cylinder, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(2.6f, 0.5f, 2.6f), mDark);
            P(torso, Shape.Box, new Vector3(0f, 1.0f, 0f), Vector3.zero, new Vector3(2.4f, 1.3f, 2.2f), mDark);
            for (int k = 0; k < 3; k++)
                P(torso, Shape.Box, new Vector3(0f, 0.6f + k * 0.38f, 1.12f), Vector3.zero, new Vector3(2.0f - k * 0.1f, 0.26f, 0.2f), mSecondary);
            // V-shaped chest: wide shoulders tapering to the waist
            P(torso, Shape.Box, new Vector3(0f, 2.9f, -0.2f), Vector3.zero, new Vector3(4.8f, 2.4f, 3.2f), mSecondary);
            for (int s = -1; s <= 1; s += 2)
            {
                Armor(torso, new Vector3(s * 1.2f, 2.75f, 1.45f), new Vector3(-8f, -s * 16f, -s * 12f), new Vector3(2.3f, 2.4f, 0.45f), mPrimary);
                P(torso, Shape.Box, new Vector3(s * 1.25f, 3.65f, 1.72f), new Vector3(-8f, -s * 16f, -s * 12f), new Vector3(1.6f, 0.14f, 0.05f), mAccent);
            }
            P(torso, Shape.Box, new Vector3(0f, 1.75f, 1.25f), new Vector3(20f, 0f, 0f), new Vector3(1.6f, 0.9f, 0.4f), mPrimary);
            P(torso, Shape.Box, new Vector3(0f, 4.2f, -0.2f), Vector3.zero, new Vector3(4.2f, 0.35f, 3.0f), mPrimary);
            // Backpack: twin jump-jet housings
            P(torso, Shape.Box, new Vector3(0f, 2.6f, -2.2f), Vector3.zero, new Vector3(3.4f, 2.6f, 1.4f), mDark);
            Vent(torso, new Vector3(0f, 3.1f, -2.92f), new Vector3(0f, 180f, 0f), 1.4f, 1.1f, 4);
            for (int s = -1; s <= 1; s += 2)
            {
                Armor(torso, new Vector3(s * 1.4f, 2.4f, -2.4f), new Vector3(0f, 180f, 0f), new Vector3(1.1f, 2.6f, 0.5f), mPrimary, mDark, false);
                P(torso, Shape.Cylinder, new Vector3(s * 1.4f, 0.95f, -2.5f), Vector3.zero, new Vector3(1.0f, 0.6f, 1.0f), mMetal);
                P(torso, Shape.Cylinder, new Vector3(s * 1.4f, 0.62f, -2.5f), Vector3.zero, new Vector3(0.7f, 0.06f, 0.7f), A.Glow(new Color(1f, 0.6f, 0.2f), 2.5f));
            }

            // Helmet head with twin eyes and a crest
            var head = Group(torso, "Head", new Vector3(0f, 4.75f, 0.55f));
            P(head, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(1.5f, 1.15f, 1.6f), mSecondary);
            P(head, Shape.Box, new Vector3(0f, 0.45f, 0f), new Vector3(-8f, 0f, 0f), new Vector3(1.6f, 0.35f, 1.75f), mPrimary);
            P(head, Shape.Box, new Vector3(0f, 0.75f, -0.1f), Vector3.zero, new Vector3(0.18f, 0.4f, 1.5f), mAccent);
            P(head, Shape.Box, new Vector3(0f, -0.05f, 0.82f), Vector3.zero, new Vector3(1.3f, 0.36f, 0.08f), mBlack);
            for (int s = -1; s <= 1; s += 2)
                P(head, Shape.Box, new Vector3(s * 0.33f, -0.03f, 0.88f), new Vector3(0f, 0f, s * 8f), new Vector3(0.42f, 0.13f, 0.05f), mGlow);
            P(head, Shape.Box, new Vector3(0f, -0.42f, 0.7f), new Vector3(15f, 0f, 0f), new Vector3(1.0f, 0.35f, 0.3f), mDark);

            // Shoulder plates (blow off)
            var plates = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var pl = Group(upper, s < 0 ? "ShoulderL" : "ShoulderR", new Vector3(s * 3.3f, 3.8f, 0.0f));
                pl.localRotation = Quaternion.Euler(0f, 0f, -s * 20f);
                Armor(pl, Vector3.zero, new Vector3(-90f, 0f, 0f), new Vector3(2.4f, 3.0f, 0.6f), mPrimary);
                Armor(pl, new Vector3(s * 1.2f, -0.55f, 0f), new Vector3(0f, s * 90f, 0f), new Vector3(2.8f, 1.2f, 0.35f), mPrimary, mDark, false);
                P(pl, Shape.Box, new Vector3(s * 1.4f, -0.3f, 0f), Vector3.zero, new Vector3(0.06f, 0.2f, 2.2f), mAccent);
                MeshMerge.Merge(pl);
                plates.Add(pl);
            }

            // ---- Arms ----
            var arms = Group(upper, "Arms", new Vector3(0f, 3.1f, 0.1f));
            var armStatic = Group(arms, "ArmStatic", Vector3.zero);
            var mounts = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 3.3f;
                Joint(armStatic, new Vector3(x, 0f, 0f), 1.6f, 1.5f);
                P(armStatic, Shape.Box, new Vector3(x, -1.2f, 0f), Vector3.zero, new Vector3(0.95f, 2.1f, 1.05f), mDark);
                Armor(armStatic, new Vector3(x + s * 0.5f, -1.15f, 0f), new Vector3(0f, s * 90f, 0f), new Vector3(1.3f, 1.9f, 0.3f), mSecondary);
                Piston(armStatic, new Vector3(x - s * 0.45f, -0.35f, 0.4f), new Vector3(x - s * 0.45f, -2.1f, 0.5f), 0.13f);
                Joint(armStatic, new Vector3(x, -2.35f, 0.1f), 1.2f, 1.3f);
                Armor(armStatic, new Vector3(x, -2.35f, 0.8f), new Vector3(-90f, 0f, 0f), new Vector3(1.5f, 1.8f, 1.35f), mSecondary);
                mounts[s < 0 ? 0 : 1] = Group(arms, s < 0 ? "MountL" : "MountR", new Vector3(x, -2.35f, 1.45f));
            }
            // Arm shield on the outside of the left forearm
            var shield = Group(armStatic, "Shield", new Vector3(-4.35f, -2.2f, 0.9f));
            Armor(shield, Vector3.zero, new Vector3(0f, -90f, 0f), new Vector3(3.2f, 3.4f, 0.4f), mPrimary);
            P(shield, Shape.Box, new Vector3(-0.25f, 0.9f, 0f), Vector3.zero, new Vector3(0.06f, 0.25f, 2.6f), mAccent);
            P(shield, Shape.Box, new Vector3(-0.25f, -0.9f, 0f), Vector3.zero, new Vector3(0.06f, 0.25f, 2.6f), mAccent);

            var shoulder = Group(arms, "ShoulderMount", new Vector3(3.3f, 1.65f, -0.3f));
            var backs = new[]
            {
                Group(upper, "BackL", new Vector3(-1.7f, 4.45f, -1.6f)),
                Group(upper, "BackR", new Vector3(1.7f, 4.45f, -1.6f))
            };

            var jets = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var jet = Group(upper, "Jet", new Vector3(s * 1.4f, 0.5f, -2.5f));
                jet.localRotation = Quaternion.LookRotation(new Vector3(0f, -1f, -0.2f));
                jets.Add(jet);
            }
            var smoke = Group(upper, "SmokePoint", new Vector3(0f, 3.0f, -1.2f));

            MeshMerge.Merge(hull);
            MeshMerge.Merge(torso);
            MeshMerge.Merge(armStatic);
            MergeLegs(legs);

            AddDamageable(root, name, team, 2000f, 2.0f, smoke, new List<Transform> { plates[0], plates[1] });

            var walker = root.AddComponent<WalkerLocomotion>();
            walker.legs = legs.ToArray();
            walker.jetNozzles = jets.ToArray();
            walker.standHeight = stand;
            walker.thighLength = thigh;
            walker.shinLength = shin;
            walker.strideLength = 5f;
            walker.stepDuration = 0.45f;
            walker.walkSpeed = 9.5f;
            walker.turnRate = 0.95f;
            walker.jetAccel = 26f;
            walker.fuelDrain = 32f;

            var turret = root.AddComponent<Turret>();
            turret.yawPivot = upper;
            turret.pitchPivot = arms;
            turret.yawSpeed = 65f;
            turret.pitchSpeed = 36f;
            turret.maxYaw = 115f;
            turret.acquireCone = player ? 75f : 180f;

            return new Chassis { root = root, turret = turret, armMounts = mounts, shoulderMount = shoulder, backMounts = backs, armScale = 0.92f, shoulderScale = 0.9f, backScale = 0.85f };
        }
    }
}
