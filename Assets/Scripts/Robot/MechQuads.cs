using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public static partial class RobotFactory
    {
        /// <summary>Wide spider foot: a round pad with three claws and an ankle joint.</summary>
        static void PadFoot(Transform foot, float d)
        {
            P(foot, Shape.Cylinder, new Vector3(0f, -0.15f, 0f), Vector3.zero, new Vector3(d, 0.4f, d), mDark);
            P(foot, Shape.Hex, new Vector3(0f, 0.2f, 0f), Vector3.zero, new Vector3(d * 0.7f, 0.45f, d * 0.7f), mSecondary);
            for (int k = 0; k < 3; k++)
            {
                var claw = Group(foot, "Claw", new Vector3(0f, -0.2f, 0f));
                claw.localRotation = Quaternion.Euler(0f, k * 120f, 0f);
                P(claw, Shape.Wedge, new Vector3(0f, 0f, d * 0.6f), Vector3.zero, new Vector3(0.45f, 0.35f, d * 0.55f), mMetal);
            }
            Joint(foot, new Vector3(0f, 0.45f, 0f), d * 0.45f, d * 0.5f);
        }

        // =====================================================================
        // ARACHNE: four-legged spider walker. Armoured thorax with a cluster of
        // sensor eyes, a swollen rear abdomen and a turreted weapon head.
        // =====================================================================

        static Chassis BuildArachne(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            const float stand = 6.5f;
            const float thigh = 4.6f;
            const float shin = 5.6f;

            var root = NewRoot(name, groundPos + Vector3.up * stand, rotation, 36000f, false);
            var t = root.transform;
            AddBox(t, new Vector3(0f, 0.6f, -0.6f), new Vector3(6f, 3.2f, 8f));

            // ---- Body ----
            var body = Group(t, "BodyStatic", Vector3.zero);
            P(body, Shape.Box, new Vector3(0f, 0.2f, 0.6f), Vector3.zero, new Vector3(4.6f, 1.8f, 4.4f), mDark);
            P(body, Shape.Hex, new Vector3(0f, -0.3f, 0.6f), Vector3.zero, new Vector3(5.4f, 0.9f, 5.0f), mSecondary);
            Armor(body, new Vector3(0f, 1.2f, 0.6f), new Vector3(-90f, 0f, 0f), new Vector3(4.0f, 4.0f, 0.4f), mPrimary);
            Armor(body, new Vector3(0f, 0.3f, 2.95f), new Vector3(-25f, 0f, 0f), new Vector3(3.6f, 1.8f, 0.4f), mPrimary, mDark, false);
            // Sensor eye cluster in a dark visor
            P(body, Shape.Box, new Vector3(0f, 0.45f, 3.2f), new Vector3(-25f, 0f, 0f), new Vector3(2.4f, 1.0f, 0.2f), mBlack);
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 3 - 1) * 0.62f;
                float y = 0.25f + (i / 3) * 0.45f;
                P(body, Shape.Sphere, new Vector3(x, y, 3.3f - (i / 3) * 0.2f), Vector3.zero, Vector3.one * (i / 3 == 0 ? 0.4f : 0.28f), mGlow);
            }
            // Abdomen: armoured segments over a dark core
            P(body, Shape.Sphere, new Vector3(0f, 0.6f, -3.0f), Vector3.zero, new Vector3(5.0f, 3.2f, 5.4f), mDark);
            for (int k = 0; k < 4; k++)
                Armor(body, new Vector3(0f, 2.0f - Mathf.Abs(k - 1.5f) * 0.3f, -1.4f - k * 1.05f), new Vector3(-90f + (k - 1.5f) * 18f, 0f, 0f),
                    new Vector3(3.8f - Mathf.Abs(k - 1.5f) * 0.5f, 1.15f, 0.35f), k % 2 == 0 ? mPrimary : mSecondary, mDark, false);
            for (int s = -1; s <= 1; s += 2)
            {
                Armor(body, new Vector3(s * 2.25f, 0.8f, -3.0f), new Vector3(0f, s * 90f, s * 20f), new Vector3(3.6f, 1.8f, 0.35f), mPrimary, mDark, false);
                Vent(body, new Vector3(s * 2.35f, 0.15f, 0.6f), new Vector3(0f, s * 90f, 0f), 2.4f, 0.9f, 3);
                P(body, Shape.Cylinder, new Vector3(s * 2.45f, 0.6f, -0.8f), new Vector3(90f, 0f, 0f), new Vector3(0.22f, 3.6f, 0.22f), mBlack);
            }
            Lamp(body, new Vector3(0f, -0.4f, 3.0f), new Vector3(15f, 0f, 0f), 0.5f, A.Glow(Color.white, 3f));

            // ---- Legs, knees high like a spider ----
            var legs = new List<WalkerLocomotion.Leg>();
            var corners = new[] { new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, -1f) };
            foreach (var c in corners)
            {
                float s = c.x;
                Vector3 hp = new Vector3(s * 2.6f, 0f, c.y * 1.8f - 0.4f);
                var hip = Group(t, "Hip", hp);
                P(body, Shape.Sphere, hp, Vector3.zero, Vector3.one * 1.5f, mDark);
                P(body, Shape.Cylinder, hp + new Vector3(s * 0.3f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(1.2f, 0.5f, 1.2f), mMetal);

                var thighT = Group(t, "Thigh", Vector3.zero);
                LegSegment(thighT, thigh, 1.1f, 1.2f, s, false);

                var kneeT = Group(t, "Knee", Vector3.zero);
                Joint(kneeT, Vector3.zero, 1.35f, 1.1f);
                P(kneeT, Shape.Wedge, new Vector3(0f, 0.95f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0.7f, 1.4f, 0.7f), mPrimary);
                P(kneeT, Shape.Sphere, new Vector3(0f, 0.3f, 0.6f), Vector3.zero, Vector3.one * 0.28f, mGlow);

                var shinT = Group(t, "Shin", Vector3.zero);
                LegSegment(shinT, shin, 0.95f, 1.05f, s, true);

                var footT = Group(t, "Foot", Vector3.zero);
                PadFoot(footT, 1.8f);

                legs.Add(new WalkerLocomotion.Leg
                {
                    hip = hip, thigh = thighT, knee = kneeT, shin = shinT, foot = footT, side = s,
                    footOffset = new Vector3(s * 4.6f, 0f, c.y * 3.0f),
                    bendHint = new Vector3(s * 0.5f, 1f, c.y * 0.3f)
                });
            }

            // ---- Weapon head ----
            var head = Group(t, "Head", new Vector3(0f, 1.6f, 1.0f));
            var hs = Group(head, "HeadStatic", Vector3.zero);
            P(hs, Shape.Cylinder, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(2.8f, 0.3f, 2.8f), mDark);
            P(hs, Shape.Hex, new Vector3(0f, 0.65f, 0f), Vector3.zero, new Vector3(2.6f, 0.9f, 2.8f), mSecondary);
            Armor(hs, new Vector3(0f, 0.85f, 1.25f), new Vector3(-30f, 0f, 0f), new Vector3(1.8f, 0.9f, 0.3f), mPrimary, mDark, false);
            P(hs, Shape.Box, new Vector3(0f, 1.2f, -0.3f), Vector3.zero, new Vector3(1.4f, 0.2f, 1.4f), mPrimary);

            var guns = Group(head, "Guns", new Vector3(0f, 0.7f, 0.6f));
            var gs = Group(guns, "GunStatic", Vector3.zero);
            var mounts = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                Joint(gs, new Vector3(s * 1.45f, 0f, 0f), 1.0f, 0.5f);
                P(gs, Shape.Box, new Vector3(s * 1.75f, 0f, 0.1f), Vector3.zero, new Vector3(0.5f, 0.8f, 1.2f), mDark);
                mounts[s < 0 ? 0 : 1] = Group(guns, s < 0 ? "MountL" : "MountR", new Vector3(s * 1.75f, 0f, 0.5f));
            }
            var shoulder = Group(guns, "ShoulderMount", new Vector3(0f, 0.85f, -0.2f));
            var backs = new[] { Group(t, "BackMount", new Vector3(0f, 2.4f, -3.0f)) };

            // ---- Detachable armour ----
            var plates = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var plate = Group(t, "SidePlate", new Vector3(s * 2.75f, 0.9f, 1.0f));
                plate.localRotation = Quaternion.Euler(0f, s * 90f, -s * 20f);
                Armor(plate, Vector3.zero, Vector3.zero, new Vector3(2.6f, 1.2f, 0.3f), mPrimary);
                MeshMerge.Merge(plate);
                plates.Add(plate);
            }
            var mast = Group(t, "SensorMast", new Vector3(1.4f, 1.6f, -1.2f));
            Antenna(mast, Vector3.zero, 2.0f);
            MeshMerge.Merge(mast);

            var jets = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var jet = Group(t, "Jet", new Vector3(s * 1.2f, -1.0f, -3.0f));
                jet.localRotation = Quaternion.LookRotation(Vector3.down);
                jets.Add(jet);
            }
            var smoke = Group(t, "SmokePoint", new Vector3(0f, 2.2f, -2.4f));

            Ladder(body, new Vector3(2.4f, -0.7f, 2.2f), 1.9f, 90f);
            Hatch(body, new Vector3(0f, 1.0f, -4.2f), new Vector3(-60f, 180f, 0f), 0.8f);
            MeshMerge.Merge(body);
            MeshMerge.Merge(hs);
            MeshMerge.Merge(gs);
            MergeLegs(legs);

            AddDamageable(root, name, team, 2100f, 2.2f, smoke, new List<Transform> { plates[0], mast, plates[1] });

            var walker = root.AddComponent<WalkerLocomotion>();
            walker.legs = legs.ToArray();
            walker.jetNozzles = jets.ToArray();
            walker.standHeight = stand;
            walker.thighLength = thigh;
            walker.shinLength = shin;
            walker.strideLength = 4.6f;
            walker.stepDuration = 0.36f;
            walker.stepHeight = 1.6f;
            walker.walkSpeed = 10f;
            walker.acceleration = 4.5f;
            walker.turnRate = 1.15f;
            walker.fuelDrain = 40f;

            var turret = root.AddComponent<Turret>();
            turret.yawPivot = head;
            turret.pitchPivot = guns;
            turret.yawSpeed = 85f;
            turret.pitchSpeed = 45f;
            turret.maxYaw = 160f;
            turret.maxPitch = 35f;
            turret.acquireCone = player ? 80f : 180f;

            return new Chassis { root = root, turret = turret, armMounts = mounts, shoulderMount = shoulder, backMounts = backs, armScale = 0.85f, shoulderScale = 0.8f, backScale = 0.9f };
        }

        // =====================================================================
        // MAMMOTH: heavy four-legged assault walker. Long armoured body on
        // animal-like legs (front knees forward, rear hocks back), a command
        // cab at the front with weapon arms, and a weapons deck on the back.
        // =====================================================================

        static Chassis BuildMammoth(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            const float stand = 7.2f;
            const float thigh = 4.4f;
            const float shin = 4.6f;

            var root = NewRoot(name, groundPos + Vector3.up * stand, rotation, 58000f, false);
            var t = root.transform;
            AddBox(t, new Vector3(0f, 0.9f, 0f), new Vector3(5.6f, 3.4f, 10.4f));
            AddBox(t, new Vector3(0f, 3.2f, 2.6f), new Vector3(4.0f, 2.0f, 3.6f));

            // ---- Body ----
            var body = Group(t, "BodyStatic", Vector3.zero);
            P(body, Shape.Box, new Vector3(0f, 0.6f, 0f), Vector3.zero, new Vector3(4.4f, 2.4f, 9.4f), mDark);
            Armor(body, new Vector3(0f, 1.95f, -0.6f), new Vector3(-90f, 0f, 0f), new Vector3(4.8f, 7.6f, 0.5f), mPrimary);
            for (int s = -1; s <= 1; s += 2)
            {
                // Long side armour in three overlapping sections
                for (int k = 0; k < 3; k++)
                    Armor(body, new Vector3(s * 2.45f, 0.75f, 3.0f - k * 3.0f), new Vector3(0f, s * 90f, 0f), new Vector3(3.3f, 2.2f, 0.4f), k == 1 ? mSecondary : mPrimary);
                P(body, Shape.Box, new Vector3(s * 2.7f, 1.6f, 0f), Vector3.zero, new Vector3(0.06f, 0.2f, 8.0f), mAccent);
            }
            // Armoured prow and rear
            Armor(body, new Vector3(0f, 0.4f, 4.85f), new Vector3(-20f, 0f, 0f), new Vector3(4.4f, 2.4f, 0.5f), mPrimary);
            Armor(body, new Vector3(0f, 0.5f, -4.85f), new Vector3(10f, 180f, 0f), new Vector3(4.2f, 2.2f, 0.45f), mPrimary);
            Vent(body, new Vector3(0f, 0.9f, -5.15f), new Vector3(10f, 180f, 0f), 2.6f, 1.2f, 4);
            for (int s = -1; s <= 1; s += 2)
            {
                P(body, Shape.Cylinder, new Vector3(s * 1.6f, 3.0f, -3.8f), Vector3.zero, new Vector3(0.6f, 1.8f, 0.6f), mDark);
                P(body, Shape.Cylinder, new Vector3(s * 1.6f, 3.92f, -3.8f), Vector3.zero, new Vector3(0.48f, 0.08f, 0.48f), mHot);
                Lamp(body, new Vector3(s * 1.5f, 0.6f, 5.05f), new Vector3(-20f, 0f, 0f), 0.45f, A.Glow(Color.white, 3f));
            }

            // ---- Legs ----
            var legs = new List<WalkerLocomotion.Leg>();
            var corners = new[] { new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, -1f) };
            foreach (var c in corners)
            {
                float s = c.x;
                bool front = c.y > 0f;
                Vector3 hp = new Vector3(s * 2.55f, -0.4f, c.y * 3.4f);
                var hip = Group(t, "Hip", hp);
                Joint(body, hp, 2.0f, 1.0f);
                Armor(body, hp + new Vector3(s * 0.55f, 0.9f, 0f), new Vector3(-90f, 0f, s * 30f), new Vector3(2.2f, 2.6f, 0.4f), mPrimary, mDark, false);

                var thighT = Group(t, "Thigh", Vector3.zero);
                LegSegment(thighT, thigh, 1.6f, 1.8f, s, false);

                var kneeT = Group(t, "Knee", Vector3.zero);
                Joint(kneeT, Vector3.zero, 1.7f, 1.5f);
                Armor(kneeT, new Vector3(0f, 0.3f, front ? 0.9f : -0.9f), new Vector3(front ? -20f : 20f, front ? 0f : 180f, 0f), new Vector3(1.6f, 1.6f, 0.45f), mPrimary);

                var shinT = Group(t, "Shin", Vector3.zero);
                LegSegment(shinT, shin, 1.5f, 1.7f, s, true);

                var footT = Group(t, "Foot", Vector3.zero);
                PadFoot(footT, 2.4f);

                legs.Add(new WalkerLocomotion.Leg
                {
                    hip = hip, thigh = thighT, knee = kneeT, shin = shinT, foot = footT, side = s,
                    footOffset = new Vector3(s * 3.0f, 0f, c.y * 3.6f),
                    bendHint = front ? new Vector3(0f, 0f, 1f) : new Vector3(0f, 0f, -1f)
                });
            }

            // ---- Command cab with weapon arms (turns to aim) ----
            var cab = Group(t, "Cab", new Vector3(0f, 2.2f, 2.6f));
            var cs = Group(cab, "CabStatic", Vector3.zero);
            P(cs, Shape.Cylinder, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(3.6f, 0.4f, 3.6f), mDark);
            P(cs, Shape.Box, new Vector3(0f, 0.9f, 0f), Vector3.zero, new Vector3(3.4f, 1.4f, 3.2f), mSecondary);
            Cockpit(cs, new Vector3(0f, 1.1f, 1.3f), 2.2f, 1.1f, 1.4f);
            Armor(cs, new Vector3(0f, 1.75f, -0.2f), new Vector3(-90f, 0f, 0f), new Vector3(3.2f, 3.0f, 0.3f), mPrimary, mDark, false);
            var antenna = Antenna(cab, new Vector3(-1.2f, 1.9f, -1.0f), 2.2f);

            var arms = Group(cab, "Arms", new Vector3(0f, 0.9f, 0.4f));
            var armStatic = Group(arms, "ArmStatic", Vector3.zero);
            var mounts = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 2.4f;
                Joint(armStatic, new Vector3(x, 0f, 0f), 1.5f, 1.2f);
                Armor(armStatic, new Vector3(x, -0.1f, 0.8f), new Vector3(-90f, 0f, 0f), new Vector3(1.5f, 1.8f, 1.3f), mSecondary);
                mounts[s < 0 ? 0 : 1] = Group(arms, s < 0 ? "MountL" : "MountR", new Vector3(x, -0.1f, 1.45f));
            }
            var shoulder = Group(arms, "ShoulderMount", new Vector3(0f, 1.55f, -0.6f));
            var backs = new[]
            {
                Group(t, "BackL", new Vector3(-1.3f, 2.3f, -2.2f)),
                Group(t, "BackR", new Vector3(1.3f, 2.3f, -2.2f))
            };

            // Detachable deck plates
            var plates = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var pl = Group(t, "DeckPlate", new Vector3(s * 1.4f, 2.35f, -0.2f));
                Armor(pl, Vector3.zero, new Vector3(-90f, 0f, 0f), new Vector3(2.2f, 2.0f, 0.3f), mSecondary);
                MeshMerge.Merge(pl);
                plates.Add(pl);
            }

            var jets = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var jet = Group(t, "Jet", new Vector3(s * 1.6f, -0.8f, -1.0f));
                jet.localRotation = Quaternion.LookRotation(Vector3.down);
                jets.Add(jet);
            }
            var smoke = Group(t, "SmokePoint", new Vector3(0f, 2.4f, -2.8f));

            Railing(body, new Vector3(0f, 2.2f, -2.6f), 4.2f, 3.2f);
            Ladder(body, new Vector3(2.9f, -0.6f, -3.0f), 2.8f, 90f);
            Ladder(body, new Vector3(-2.9f, -0.6f, -3.0f), 2.8f, -90f);
            Hatch(body, new Vector3(0f, 2.23f, -1.5f), new Vector3(-90f, 0f, 0f), 1.0f);
            Stencil(body, new Vector3(2.88f, 1.2f, 1.0f), new Vector3(0f, 90f, 0f), 1.6f);
            MeshMerge.Merge(body);
            MeshMerge.Merge(cs);
            MeshMerge.Merge(armStatic);
            MeshMerge.Merge(antenna);
            MergeLegs(legs);

            AddDamageable(root, name, team, 3300f, 2.7f, smoke, new List<Transform> { plates[0], antenna, plates[1] });

            var walker = root.AddComponent<WalkerLocomotion>();
            walker.legs = legs.ToArray();
            walker.jetNozzles = jets.ToArray();
            walker.standHeight = stand;
            walker.thighLength = thigh;
            walker.shinLength = shin;
            walker.strideLength = 5.2f;
            walker.stepDuration = 0.5f;
            walker.stepHeight = 1.5f;
            walker.walkSpeed = 7f;
            walker.acceleration = 2.8f;
            walker.turnRate = 0.65f;
            walker.jetAccel = 18f;
            walker.fuelDrain = 20f;
            walker.sprintBoost = true;

            var turret = root.AddComponent<Turret>();
            turret.yawPivot = cab;
            turret.pitchPivot = arms;
            turret.yawSpeed = 50f;
            turret.pitchSpeed = 30f;
            turret.maxYaw = 150f;
            turret.acquireCone = player ? 75f : 180f;

            return new Chassis { root = root, turret = turret, armMounts = mounts, shoulderMount = shoulder, backMounts = backs, armScale = 1.0f, shoulderScale = 1.0f, backScale = 1.0f };
        }
    }
}
