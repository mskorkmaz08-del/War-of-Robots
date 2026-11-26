using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public static partial class RobotFactory
    {
        // =====================================================================
        // LANCER: tall, slim sniper walker. Long armoured legs with forward
        // knees like a soldier's, a narrow angular torso and a one-eyed sensor head.
        // =====================================================================

        static Chassis BuildLancer(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            const float stand = 7.6f;
            const float thigh = 4.5f;
            const float shin = 4.8f;

            var root = NewRoot(name, groundPos + Vector3.up * stand, rotation, 32000f, false);
            var t = root.transform;
            AddBox(t, new Vector3(0f, 2.6f, 0.1f), new Vector3(5.0f, 5.0f, 4.0f));
            AddBox(t, new Vector3(0f, -3.4f, 0f), new Vector3(3.6f, 5f, 3f));

            // ---- Pelvis ----
            var hull = Group(t, "HullStatic", Vector3.zero);
            P(hull, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(2.6f, 1.3f, 2.2f), mDark);
            Armor(hull, new Vector3(0f, -0.3f, 1.2f), new Vector3(14f, 0f, 0f), new Vector3(1.5f, 1.2f, 0.35f), mPrimary);
            Armor(hull, new Vector3(0f, -0.2f, -1.2f), new Vector3(-10f, 180f, 0f), new Vector3(1.8f, 1.0f, 0.3f), mPrimary);
            P(hull, Shape.Cylinder, new Vector3(0f, 0.8f, 0f), Vector3.zero, new Vector3(2.2f, 0.4f, 2.2f), mDark);
            for (int s = -1; s <= 1; s += 2)
            {
                Joint(hull, new Vector3(s * 1.7f, -0.25f, 0f), 1.6f, 0.7f);
                Armor(hull, new Vector3(s * 1.55f, 0.35f, 0f), new Vector3(0f, s * 90f, s * 25f), new Vector3(2.0f, 0.9f, 0.3f), mSecondary);
            }

            // ---- Legs: forward knees ----
            var legs = new List<WalkerLocomotion.Leg>();
            for (int s = -1; s <= 1; s += 2)
            {
                var hip = Group(t, s < 0 ? "HipL" : "HipR", new Vector3(s * 2.0f, -0.25f, 0f));

                var thighT = Group(t, "Thigh", Vector3.zero);
                LegSegment(thighT, thigh, 1.3f, 1.5f, s, false);

                var kneeT = Group(t, "Knee", Vector3.zero);
                Joint(kneeT, Vector3.zero, 1.45f, 1.3f);
                Armor(kneeT, new Vector3(0f, 0.35f, 0.75f), new Vector3(-25f, 0f, 0f), new Vector3(1.3f, 1.5f, 0.4f), mPrimary);

                var shinT = Group(t, "Shin", Vector3.zero);
                LegSegment(shinT, shin, 1.25f, 1.4f, s, true);
                P(shinT, Shape.Box, new Vector3(0f, 0.95f, -0.6f), Vector3.zero, new Vector3(0.5f, 0.12f, 1.0f), mGlow);

                var footT = Group(t, "Foot", Vector3.zero);
                MechFoot(footT, 2.3f, 4.0f, mPrimary);

                legs.Add(new WalkerLocomotion.Leg { hip = hip, thigh = thighT, knee = kneeT, shin = shinT, foot = footT, side = s });
            }

            // ---- Upper body ----
            var upper = Group(t, "Upper", new Vector3(0f, 1.1f, 0f));
            var torso = Group(upper, "TorsoStatic", Vector3.zero);
            P(torso, Shape.Cylinder, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(2.4f, 0.5f, 2.4f), mDark);
            P(torso, Shape.Box, new Vector3(0f, 0.9f, 0f), Vector3.zero, new Vector3(2.4f, 1.1f, 2.2f), mDark);
            // Angular chest: narrow at the waist, wide at the shoulders
            P(torso, Shape.Box, new Vector3(0f, 2.5f, -0.2f), Vector3.zero, new Vector3(4.0f, 2.4f, 3.0f), mSecondary);
            Armor(torso, new Vector3(0f, 2.55f, 1.4f), new Vector3(-14f, 0f, 0f), new Vector3(3.2f, 2.2f, 0.4f), mPrimary);
            for (int s = -1; s <= 1; s += 2)
                Armor(torso, new Vector3(s * 1.15f, 1.35f, 1.05f), new Vector3(10f, -s * 22f, 0f), new Vector3(1.3f, 1.2f, 0.3f), mPrimary, mDark, false);
            P(torso, Shape.Box, new Vector3(0f, 3.75f, -0.3f), Vector3.zero, new Vector3(3.4f, 0.3f, 2.6f), mPrimary);
            // Back pack with vents
            P(torso, Shape.Box, new Vector3(0f, 2.3f, -2.1f), Vector3.zero, new Vector3(2.8f, 2.2f, 1.4f), mDark);
            Vent(torso, new Vector3(0f, 2.3f, -2.82f), new Vector3(0f, 180f, 0f), 2.2f, 1.6f, 5);
            for (int s = -1; s <= 1; s += 2)
                P(torso, Shape.Cylinder, new Vector3(s * 1.0f, 0.95f, -2.2f), new Vector3(20f, 0f, 0f), new Vector3(0.8f, 0.7f, 0.8f), mMetal);

            // One-eyed sensor head
            var head = Group(torso, "Head", new Vector3(0f, 4.4f, 0.5f));
            P(head, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(1.5f, 1.0f, 1.8f), mSecondary);
            P(head, Shape.Box, new Vector3(0f, 0.45f, 0.1f), new Vector3(-10f, 0f, 0f), new Vector3(1.6f, 0.25f, 1.9f), mPrimary);
            P(head, Shape.Cylinder, new Vector3(0f, -0.02f, 0.95f), new Vector3(90f, 0f, 0f), new Vector3(0.8f, 0.3f, 0.8f), mBlack);
            P(head, Shape.Cylinder, new Vector3(0f, -0.02f, 1.12f), new Vector3(90f, 0f, 0f), new Vector3(0.52f, 0.06f, 0.52f), mGlow);
            for (int s = -1; s <= 1; s += 2)
                P(head, Shape.Box, new Vector3(s * 0.85f, 0.1f, -0.3f), new Vector3(0f, 0f, -s * 10f), new Vector3(0.12f, 0.8f, 1.4f), mPrimary);
            var antenna = Antenna(upper, new Vector3(0.6f, 4.9f, -0.4f), 1.8f);

            // Shoulder plates (blow off)
            var plates = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var pl = Group(upper, s < 0 ? "ShoulderL" : "ShoulderR", new Vector3(s * 2.75f, 3.55f, 0.0f));
                pl.localRotation = Quaternion.Euler(0f, 0f, -s * 18f);
                Armor(pl, Vector3.zero, new Vector3(-90f, 0f, 0f), new Vector3(1.9f, 2.6f, 0.5f), mPrimary);
                P(pl, Shape.Box, new Vector3(s * 0.96f, -0.1f, 0f), Vector3.zero, new Vector3(0.05f, 0.25f, 2.0f), mAccent);
                MeshMerge.Merge(pl);
                plates.Add(pl);
            }

            // ---- Arms ----
            var arms = Group(upper, "Arms", new Vector3(0f, 2.9f, 0.1f));
            var armStatic = Group(arms, "ArmStatic", Vector3.zero);
            var mounts = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 2.75f;
                Joint(armStatic, new Vector3(x, 0f, 0f), 1.4f, 1.3f);
                P(armStatic, Shape.Box, new Vector3(x, -1.1f, 0f), Vector3.zero, new Vector3(0.8f, 1.9f, 0.9f), mDark);
                Armor(armStatic, new Vector3(x + s * 0.42f, -1.0f, 0f), new Vector3(0f, s * 90f, 0f), new Vector3(1.1f, 1.7f, 0.28f), mSecondary);
                Piston(armStatic, new Vector3(x - s * 0.4f, -0.3f, 0.35f), new Vector3(x - s * 0.4f, -1.9f, 0.45f), 0.11f);
                Joint(armStatic, new Vector3(x, -2.15f, 0.1f), 1.05f, 1.1f);
                Armor(armStatic, new Vector3(x, -2.15f, 0.75f), new Vector3(-90f, 0f, 0f), new Vector3(1.3f, 1.6f, 1.15f), mSecondary);
                mounts[s < 0 ? 0 : 1] = Group(arms, s < 0 ? "MountL" : "MountR", new Vector3(x, -2.15f, 1.3f));
            }
            var shoulder = Group(arms, "ShoulderMount", new Vector3(-2.75f, 1.5f, -0.3f));
            var backs = new[]
            {
                Group(upper, "BackL", new Vector3(-1.5f, 3.95f, -1.9f)),
                Group(upper, "BackR", new Vector3(1.5f, 3.95f, -1.9f))
            };

            var jets = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var jet = Group(upper, "Jet", new Vector3(s * 1.0f, 0.7f, -2.3f));
                jet.localRotation = Quaternion.LookRotation(new Vector3(0f, -1f, -0.3f));
                jets.Add(jet);
            }
            var smoke = Group(upper, "SmokePoint", new Vector3(0f, 2.6f, -1f));

            MeshMerge.Merge(hull);
            MeshMerge.Merge(torso);
            MeshMerge.Merge(armStatic);
            MeshMerge.Merge(antenna);
            MergeLegs(legs);

            AddDamageable(root, name, team, 1700f, 1.8f, smoke, new List<Transform> { plates[0], antenna, plates[1] });

            var walker = root.AddComponent<WalkerLocomotion>();
            walker.legs = legs.ToArray();
            walker.jetNozzles = jets.ToArray();
            walker.standHeight = stand;
            walker.thighLength = thigh;
            walker.shinLength = shin;
            walker.strideLength = 5f;
            walker.stepDuration = 0.42f;
            walker.walkSpeed = 11f;
            walker.turnRate = 1.0f;
            walker.fuelDrain = 35f;

            var turret = root.AddComponent<Turret>();
            turret.yawPivot = upper;
            turret.pitchPivot = arms;
            turret.yawSpeed = 70f;
            turret.pitchSpeed = 40f;
            turret.maxYaw = 120f;
            turret.acquireCone = player ? 70f : 180f;

            return new Chassis { root = root, turret = turret, armMounts = mounts, shoulderMount = shoulder, backMounts = backs, armScale = 0.85f, shoulderScale = 0.85f, backScale = 0.8f };
        }
    }
}
