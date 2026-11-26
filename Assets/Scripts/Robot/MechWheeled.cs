using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public static partial class RobotFactory
    {
        // =====================================================================
        // WHEELED ROBOTS: an armoured drive base carrying a real robot upper
        // body (chest, head, shoulders and gun arms), like a mech that rides
        // on a wheeled undercarriage instead of walking. The torso turns to
        // aim and the arms pitch, so weapons read as held, not bolted on.
        // =====================================================================

        class UpperBody
        {
            public Transform upper, torso, arms, armStatic, shoulder, antenna, smoke;
            public Transform[] mounts = new Transform[2];
            public Transform[] backs = new Transform[2];
            public List<Transform> plates = new List<Transform>();
        }

        /// <summary>
        /// A robot torso on a waist ring at <paramref name="waist"/>, scaled by k.
        /// headStyle: 0 cockpit canopy, 1 visor helmet, 2 one-eyed sensor head.
        /// </summary>
        static UpperBody WheeledUpper(Transform t, Vector3 waist, float k, int headStyle, Material chest)
        {
            var u = new UpperBody();
            u.upper = Group(t, "Upper", waist);
            var torso = u.torso = Group(u.upper, "TorsoStatic", Vector3.zero);
            P(torso, Shape.Cylinder, new Vector3(0f, 0.12f, 0f) * k, Vector3.zero, new Vector3(2.6f, 0.45f, 2.6f) * k, mDark);
            P(torso, Shape.Box, new Vector3(0f, 0.85f, 0f) * k, Vector3.zero, new Vector3(2.2f, 1.1f, 2.0f) * k, mDark);
            for (int s = -1; s <= 1; s += 2)
                Piston(torso, new Vector3(s * 0.8f, 0.3f, 0.8f) * k, new Vector3(s * 1.1f, 1.5f, 1.0f) * k, 0.12f * k);
            // Chest: wide at the shoulders, armoured front
            P(torso, Shape.Box, new Vector3(0f, 2.4f, -0.2f) * k, Vector3.zero, new Vector3(4.2f, 2.2f, 3.0f) * k, chest);
            Armor(torso, new Vector3(0f, 2.45f, 1.35f) * k, new Vector3(-12f, 0f, 0f), new Vector3(3.4f, 2.0f, 0.4f) * k, mPrimary);
            for (int s = -1; s <= 1; s += 2)
                Armor(torso, new Vector3(s * 1.1f, 1.25f, 0.95f) * k, new Vector3(10f, -s * 20f, 0f), new Vector3(1.2f, 1.0f, 0.3f) * k, mPrimary, mDark, false);
            P(torso, Shape.Box, new Vector3(0f, 3.6f, -0.3f) * k, Vector3.zero, new Vector3(3.6f, 0.3f, 2.6f) * k, mPrimary);
            // Back pack with vents and a reactor glow
            P(torso, Shape.Box, new Vector3(0f, 2.2f, -2.0f) * k, Vector3.zero, new Vector3(2.8f, 2.0f, 1.2f) * k, mDark);
            Vent(torso, new Vector3(0f, 2.4f, -2.62f) * k, new Vector3(0f, 180f, 0f), 2.0f * k, 1.2f * k, 4);
            P(torso, Shape.Cylinder, new Vector3(0f, 1.45f, -2.62f) * k, new Vector3(90f, 0f, 0f), new Vector3(0.9f, 0.08f, 0.9f) * k, mGlow);
            for (int s = -1; s <= 1; s += 2)
                Vent(torso, new Vector3(s * 2.12f, 2.3f, -0.4f) * k, new Vector3(0f, s * 90f, 0f), 1.8f * k, 1.2f * k, 3);

            // Head
            var hp = new Vector3(0f, 4.25f, 0.35f) * k;
            switch (headStyle)
            {
                case 0:
                    Cockpit(torso, hp + new Vector3(0f, 0.1f, 0.2f) * k, 2.0f * k, 1.2f * k, 1.9f * k);
                    break;
                case 1:
                {
                    var head = Group(torso, "Head", hp);
                    P(head, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(1.5f, 1.1f, 1.6f) * k, mSecondary);
                    P(head, Shape.Box, new Vector3(0f, 0.5f, 0f) * k, new Vector3(-8f, 0f, 0f), new Vector3(1.7f, 0.3f, 1.8f) * k, mPrimary);
                    P(head, Shape.Box, new Vector3(0f, 0.05f, 0.82f) * k, Vector3.zero, new Vector3(1.25f, 0.28f, 0.1f) * k, mGlow);
                    P(head, Shape.Box, new Vector3(0f, -0.38f, 0.78f) * k, new Vector3(20f, 0f, 0f), new Vector3(0.9f, 0.35f, 0.3f) * k, mDark);
                    for (int s = -1; s <= 1; s += 2)
                        P(head, Shape.Box, new Vector3(s * 0.82f, 0.1f, -0.1f) * k, Vector3.zero, new Vector3(0.14f, 0.9f, 1.3f) * k, mPrimary);
                    break;
                }
                default:
                {
                    var head = Group(torso, "Head", hp);
                    P(head, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(1.4f, 1.0f, 1.6f) * k, mSecondary);
                    P(head, Shape.Box, new Vector3(0f, 0.45f, 0.1f) * k, new Vector3(-10f, 0f, 0f), new Vector3(1.5f, 0.25f, 1.7f) * k, mPrimary);
                    P(head, Shape.Cylinder, new Vector3(0f, 0f, 0.86f) * k, new Vector3(90f, 0f, 0f), new Vector3(0.75f, 0.3f, 0.75f) * k, mBlack);
                    P(head, Shape.Cylinder, new Vector3(0f, 0f, 1.02f) * k, new Vector3(90f, 0f, 0f), new Vector3(0.48f, 0.06f, 0.48f) * k, mGlow);
                    break;
                }
            }
            u.antenna = Antenna(u.upper, new Vector3(0.7f, 4.6f, -0.6f) * k, 1.6f * k);

            // Shoulder plates (blow off)
            for (int s = -1; s <= 1; s += 2)
            {
                var pl = Group(u.upper, s < 0 ? "ShoulderL" : "ShoulderR", new Vector3(s * 2.8f, 3.4f, 0f) * k);
                pl.localRotation = Quaternion.Euler(0f, 0f, -s * 16f);
                Armor(pl, Vector3.zero, new Vector3(-90f, 0f, 0f), new Vector3(2.0f, 2.6f, 0.5f) * k, mPrimary);
                Armor(pl, new Vector3(s * 1.0f, -0.45f, 0f) * k, new Vector3(0f, s * 90f, 0f), new Vector3(2.4f, 1.1f, 0.35f) * k, mPrimary, mDark, false);
                P(pl, Shape.Box, new Vector3(s * 1.2f, -0.2f, 0f) * k, Vector3.zero, new Vector3(0.05f, 0.22f, 1.9f) * k, mAccent);
                MeshMerge.Merge(pl);
                u.plates.Add(pl);
            }

            // Arms (pitch to aim)
            u.arms = Group(u.upper, "Arms", new Vector3(0f, 2.8f, 0.1f) * k);
            u.armStatic = Group(u.arms, "ArmStatic", Vector3.zero);
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 2.8f;
                Joint(u.armStatic, new Vector3(x, 0f, 0f) * k, 1.5f * k, 1.4f * k);
                P(u.armStatic, Shape.Box, new Vector3(x, -1.0f, 0f) * k, Vector3.zero, new Vector3(0.9f, 1.8f, 1.0f) * k, mDark);
                Armor(u.armStatic, new Vector3(x + s * 0.48f, -0.95f, 0f) * k, new Vector3(0f, s * 90f, 0f), new Vector3(1.2f, 1.6f, 0.3f) * k, mSecondary);
                Piston(u.armStatic, new Vector3(x - s * 0.45f, -0.3f, 0.4f) * k, new Vector3(x - s * 0.45f, -1.8f, 0.5f) * k, 0.12f * k);
                Joint(u.armStatic, new Vector3(x, -2.05f, 0.1f) * k, 1.1f * k, 1.2f * k);
                Armor(u.armStatic, new Vector3(x, -2.05f, 0.75f) * k, new Vector3(-90f, 0f, 0f), new Vector3(1.4f, 1.6f, 1.2f) * k, mSecondary);
                Armor(u.armStatic, new Vector3(x + s * 0.75f, -2.05f, 0.75f) * k, new Vector3(0f, s * 90f, 0f), new Vector3(1.5f, 1.0f, 0.25f) * k, mPrimary, mDark, false);
                u.mounts[s < 0 ? 0 : 1] = Group(u.arms, s < 0 ? "MountL" : "MountR", new Vector3(x, -2.05f, 1.35f) * k);
            }
            u.shoulder = Group(u.arms, "ShoulderMount", new Vector3(-2.8f, 1.45f, -0.3f) * k);
            u.backs[0] = Group(u.upper, "BackL", new Vector3(-1.45f, 3.85f, -1.8f) * k);
            u.backs[1] = Group(u.upper, "BackR", new Vector3(1.45f, 3.85f, -1.8f) * k);
            u.smoke = Group(u.upper, "SmokePoint", new Vector3(0f, 2.4f, -1.2f) * k);
            return u;
        }

        static void MergeUpper(UpperBody u)
        {
            MeshMerge.Merge(u.torso);
            MeshMerge.Merge(u.armStatic);
            MeshMerge.Merge(u.antenna);
        }

        static Turret AddUpperTurret(GameObject root, UpperBody u, bool player, float yawSpeed, float pitchSpeed, float maxYaw)
        {
            var turret = root.AddComponent<Turret>();
            turret.yawPivot = u.upper;
            turret.pitchPivot = u.arms;
            turret.yawSpeed = yawSpeed;
            turret.pitchSpeed = pitchSpeed;
            turret.maxYaw = maxYaw;
            turret.acquireCone = player ? 80f : 180f;
            return turret;
        }

        static Chassis UpperChassis(GameObject root, Turret turret, UpperBody u, float k)
        {
            return new Chassis { root = root, turret = turret, armMounts = u.mounts, shoulderMount = u.shoulder, backMounts = u.backs, armScale = 0.82f * k, shoulderScale = 0.82f * k, backScale = 0.8f * k };
        }

        /// <summary>Rear thruster bell with a glowing ring; returns the exhaust point.</summary>
        static Transform Booster(Transform t, Transform hull, Vector3 pos, float d)
        {
            P(hull, Shape.Cylinder, pos, new Vector3(90f, 0f, 0f), new Vector3(d, 0.8f, d), mMetal);
            P(hull, Shape.Cylinder, pos + Vector3.back * 0.43f, new Vector3(90f, 0f, 0f), new Vector3(d * 0.75f, 0.05f, d * 0.75f), mHot);
            var noz = Group(t, "Booster", pos + Vector3.back * 0.5f);
            noz.localRotation = Quaternion.LookRotation(Vector3.back);
            return noz;
        }

        /// <summary>A wheel fork: an armoured strut from the hull down to the hub.</summary>
        static void WheelStrut(Transform hull, float side, Vector3 hub, float hullY)
        {
            Vector3 top = new Vector3(hub.x - side * 0.9f, hullY, hub.z);
            Piston(hull, top, new Vector3(hub.x - side * 0.6f, hub.y + 0.3f, hub.z), 0.16f);
            P(hull, Shape.Box, new Vector3(hub.x - side * 0.75f, hullY - 0.2f, hub.z), Vector3.zero, new Vector3(0.5f, 0.5f, 0.9f), mDark);
        }

        /// <summary>The raised hydraulic column that carries the torso above the deck.</summary>
        static void Pedestal(Transform hull, Vector3 waist, float deckY, float k)
        {
            float h = waist.y - deckY;
            Vector3 mid = new Vector3(waist.x, deckY + h * 0.5f, waist.z);
            P(hull, Shape.Cylinder, mid, Vector3.zero, new Vector3(1.7f * k, h, 1.7f * k), mMetal);
            P(hull, Shape.Cylinder, new Vector3(waist.x, deckY + 0.15f, waist.z), Vector3.zero, new Vector3(2.8f * k, 0.3f, 2.8f * k), mDark);
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.1f * k;
                Piston(hull, new Vector3(waist.x, deckY + 0.2f, waist.z) + o * 1.15f, new Vector3(waist.x, waist.y - 0.15f, waist.z) + o * 0.75f, 0.11f * k);
            }
        }

        // ---------------------------------------------------------------------
        // RAPTOR: six-wheeled medium raider with a visor-helmeted torso.
        // ---------------------------------------------------------------------
        static Chassis BuildRaptor(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            const float k = 0.85f;
            var root = NewRoot(name, groundPos + Vector3.up * 2.6f, rotation, 30000f, true);
            var t = root.transform;
            var waist = new Vector3(0f, 2.35f, -0.6f);
            AddBox(t, new Vector3(0f, 0.8f, 0.2f), new Vector3(5.4f, 2.0f, 10.4f));
            AddBox(t, waist + new Vector3(0f, 2.3f, -0.3f) * k, new Vector3(4.4f, 4.4f, 3.6f) * k);

            var hull = Group(t, "HullStatic", Vector3.zero);
            P(hull, Shape.Box, new Vector3(0f, 0.2f, 0f), Vector3.zero, new Vector3(3.4f, 0.8f, 8.6f), mDark);
            P(hull, Shape.Box, new Vector3(0f, 0.95f, -0.4f), Vector3.zero, new Vector3(4.4f, 1.1f, 6.4f), mSecondary);
            P(hull, Shape.Wedge, new Vector3(0f, 0.85f, 3.85f), Vector3.zero, new Vector3(4.2f, 1.3f, 2.4f), mPrimary);
            Armor(hull, new Vector3(0f, 0.45f, 4.95f), new Vector3(30f, 0f, 0f), new Vector3(3.8f, 0.9f, 0.35f), mPrimary);
            P(hull, Shape.Box, new Vector3(0f, 1.55f, -0.6f), Vector3.zero, new Vector3(3.0f, 0.2f, 3.0f), mPrimary);
            for (int s = -1; s <= 1; s += 2)
            {
                Armor(hull, new Vector3(s * 2.25f, 0.95f, -0.4f), new Vector3(0f, s * 90f, 0f), new Vector3(5.6f, 0.9f, 0.3f), mPrimary);
                P(hull, Shape.Box, new Vector3(s * 1.4f, 1.52f, 2.2f), Vector3.zero, new Vector3(0.25f, 0.05f, 2.6f), mAccent);
                Lamp(hull, new Vector3(s * 1.5f, 1.1f, 4.4f), new Vector3(-30f, 0f, 0f), 0.4f, A.Glow(Color.white, 3f));
            }
            Vent(hull, new Vector3(0f, 1.0f, -3.65f), new Vector3(0f, 180f, 0f), 2.6f, 0.8f, 3);
            Rivets(hull, new Vector3(-1.5f, 1.52f, -3.2f), new Vector3(0.75f, 0f, 0f), 5, 0.12f);
            var nozzles = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
                nozzles.Add(Booster(t, hull, new Vector3(s * 1.05f, 0.9f, -3.9f), 1.0f));

            var fenders = new List<Transform>();
            var wheels = new List<WheeledLocomotion.Wheel>();
            float[] wz = { 3.2f, 0f, -3.2f };
            foreach (float z in wz)
                for (int s = -1; s <= 1; s += 2)
                {
                    wheels.Add(BuildWheel(t, s, new Vector3(s * 3.3f, 0.2f, z), 1.4f, 1.1f));
                    WheelStrut(hull, s, new Vector3(s * 3.3f, 0.2f, z), 0.9f);
                    if (z == 0f) continue;
                    var f = Group(t, "Fender", new Vector3(s * 3.3f, 1.75f, z));
                    Armor(f, Vector3.zero, new Vector3(-90f, 0f, -s * 10f), new Vector3(1.5f, 2.8f, 0.22f), mPrimary, mDark, false);
                    P(f, Shape.Box, new Vector3(s * 0.7f, -0.2f, 0f), Vector3.zero, new Vector3(0.12f, 0.45f, 2.6f), mDark);
                    MeshMerge.Merge(f);
                    fenders.Add(f);
                }

            var u = WheeledUpper(t, waist, k, 1, mSecondary);
            Pedestal(hull, waist, 1.55f, k);
            MeshMerge.Merge(hull);
            MergeUpper(u);

            var parts = new List<Transform> { u.plates[0], fenders[0], u.antenna, fenders[3], u.plates[1], fenders[1], fenders[2] };
            AddDamageable(root, name, team, 2000f, 2f, u.smoke, parts, 0.3f);

            var drive = root.AddComponent<WheeledLocomotion>();
            drive.wheels = wheels.ToArray();
            root.GetComponent<Rigidbody>().centerOfMass = new Vector3(0f, 0.3f, 0.2f);
            drive.extraAirGravity = 0.6f;
            drive.thrusterNozzles = nozzles.ToArray();
            drive.wheelRadius = 1.4f;
            drive.suspensionTravel = 1.1f;

            var turret = AddUpperTurret(root, u, player, 90f, 45f, 150f);
            return UpperChassis(root, turret, u, k);
        }

        // ---------------------------------------------------------------------
        // HORNET: light four-wheeled scout; a slim one-eyed torso over a low
        // racing chassis with a rear wing and twin boosters.
        // ---------------------------------------------------------------------
        static Chassis BuildHornet(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            const float k = 0.68f;
            var root = NewRoot(name, groundPos + Vector3.up * 2.2f, rotation, 18000f, true);
            var t = root.transform;
            var waist = new Vector3(0f, 2.05f, -0.5f);
            AddBox(t, new Vector3(0f, 0.7f, 0f), new Vector3(4.4f, 1.6f, 8.4f));
            AddBox(t, waist + new Vector3(0f, 2.3f, -0.3f) * k, new Vector3(4.4f, 4.4f, 3.6f) * k);

            var hull = Group(t, "HullStatic", Vector3.zero);
            P(hull, Shape.Box, new Vector3(0f, 0.2f, 0f), Vector3.zero, new Vector3(3.0f, 0.6f, 7.4f), mDark);
            P(hull, Shape.Box, new Vector3(0f, 0.75f, -0.4f), Vector3.zero, new Vector3(3.6f, 1.0f, 6.0f), mSecondary);
            P(hull, Shape.Wedge, new Vector3(0f, 0.75f, 3.4f), Vector3.zero, new Vector3(3.6f, 1.0f, 2.0f), mPrimary);
            P(hull, Shape.Wedge, new Vector3(0f, 0.3f, 4.3f), Vector3.zero, new Vector3(3.8f, 0.5f, 1.2f), mSecondary);
            P(hull, Shape.Box, new Vector3(0f, 1.3f, -0.5f), Vector3.zero, new Vector3(2.4f, 0.12f, 2.4f), mPrimary);
            for (int s = -1; s <= 1; s += 2)
            {
                Armor(hull, new Vector3(s * 1.85f, 0.8f, 1.4f), new Vector3(0f, s * 90f, 0f), new Vector3(1.8f, 0.8f, 0.25f), mPrimary, mDark, false);
                Vent(hull, new Vector3(s * 1.88f, 0.75f, -1.2f), new Vector3(0f, s * 90f, 0f), 1.8f, 0.6f, 3);
                P(hull, Shape.Box, new Vector3(s * 0.55f, 1.27f, 1.8f), Vector3.zero, new Vector3(0.22f, 0.04f, 2.6f), mAccent);
                Lamp(hull, new Vector3(s * 1.35f, 0.9f, 4.05f), new Vector3(-25f, 0f, 0f), 0.35f, A.Glow(Color.white, 3f));
            }
            // Rear wing
            P(hull, Shape.Box, new Vector3(0f, 2.2f, -3.5f), new Vector3(-8f, 0f, 0f), new Vector3(4.2f, 0.12f, 1.0f), mPrimary);
            P(hull, Shape.Box, new Vector3(0f, 2.25f, -3.95f), new Vector3(-8f, 0f, 0f), new Vector3(4.2f, 0.1f, 0.12f), mAccent);
            for (int s = -1; s <= 1; s += 2)
            {
                P(hull, Shape.Box, new Vector3(s * 1.4f, 1.6f, -3.5f), Vector3.zero, new Vector3(0.12f, 1.1f, 0.6f), mDark);
                P(hull, Shape.Box, new Vector3(s * 2.1f, 2.2f, -3.5f), Vector3.zero, new Vector3(0.08f, 0.6f, 1.0f), mPrimary);
            }
            var nozzles = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
                nozzles.Add(Booster(t, hull, new Vector3(s * 0.9f, 0.8f, -3.9f), 1.0f));

            var fenders = new List<Transform>();
            var wheels = new List<WheeledLocomotion.Wheel>();
            float[] wz = { 2.6f, -2.4f };
            foreach (float z in wz)
                for (int s = -1; s <= 1; s += 2)
                {
                    wheels.Add(BuildWheel(t, s, new Vector3(s * 2.55f, 0.2f, z), 1.2f, 1.0f));
                    WheelStrut(hull, s, new Vector3(s * 2.55f, 0.2f, z), 0.75f);
                    var f = Group(t, "Fender", new Vector3(s * 2.55f, 1.55f, z));
                    Armor(f, Vector3.zero, new Vector3(-90f, 0f, -s * 10f), new Vector3(1.3f, 2.6f, 0.2f), mPrimary, mDark, false);
                    P(f, Shape.Box, new Vector3(0f, 0.12f, 1.0f), Vector3.zero, new Vector3(1.1f, 0.05f, 0.2f), mAccent);
                    MeshMerge.Merge(f);
                    fenders.Add(f);
                }

            var u = WheeledUpper(t, waist, k, 2, mSecondary);
            Pedestal(hull, waist, 1.3f, k);
            MeshMerge.Merge(hull);
            MergeUpper(u);

            var parts = new List<Transform> { fenders[0], u.plates[0], fenders[3], u.antenna, fenders[1], u.plates[1], fenders[2] };
            AddDamageable(root, name, team, 1500f, 1.6f, u.smoke, parts, 0.5f);

            var drive = root.AddComponent<WheeledLocomotion>();
            drive.wheels = wheels.ToArray();
            root.GetComponent<Rigidbody>().centerOfMass = new Vector3(0f, 0.2f, 0f);
            drive.extraAirGravity = 0.6f;
            drive.thrusterNozzles = nozzles.ToArray();
            drive.wheelRadius = 1.2f;
            drive.suspensionTravel = 0.9f;
            drive.driveAccel = 13f;
            drive.maxSpeed = 33f;
            drive.turnRate = 1.7f;
            drive.lateralGrip = 8f;
            drive.boostAccel = 30f;
            drive.boostMaxSpeed = 74f;

            var turret = AddUpperTurret(root, u, player, 120f, 60f, 170f);
            return UpperChassis(root, turret, u, k);
        }

        // ---------------------------------------------------------------------
        // JUGGERNAUT: eight-wheeled heavy with a big cockpit torso, armoured
        // skirts over the wheels and a ram plate.
        // ---------------------------------------------------------------------
        static Chassis BuildJuggernaut(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            const float k = 1.05f;
            var root = NewRoot(name, groundPos + Vector3.up * 3.0f, rotation, 45000f, false);
            var t = root.transform;
            var waist = new Vector3(0f, 2.85f, -0.9f);
            AddBox(t, new Vector3(0f, 1.0f, 0.4f), new Vector3(6.4f, 2.6f, 13.4f));
            AddBox(t, waist + new Vector3(0f, 2.3f, -0.3f) * k, new Vector3(4.4f, 4.4f, 3.6f) * k);

            var hull = Group(t, "HullStatic", Vector3.zero);
            P(hull, Shape.Box, new Vector3(0f, 0.3f, 0.2f), Vector3.zero, new Vector3(4.0f, 1.0f, 11.4f), mDark);
            P(hull, Shape.Box, new Vector3(0f, 1.25f, -0.2f), Vector3.zero, new Vector3(5.2f, 1.5f, 9.0f), mSecondary);
            P(hull, Shape.Wedge, new Vector3(0f, 1.2f, 5.3f), Vector3.zero, new Vector3(5.0f, 1.4f, 2.4f), mPrimary);
            // Ram plate
            Armor(hull, new Vector3(0f, 0.55f, 6.6f), new Vector3(20f, 0f, 0f), new Vector3(5.6f, 1.3f, 0.5f), mPrimary);
            for (int s = -1; s <= 1; s += 2)
                P(hull, Shape.Box, new Vector3(s * 1.8f, 0.35f, 7.0f), new Vector3(20f, 0f, 0f), new Vector3(0.5f, 0.9f, 0.4f), mMetal);
            P(hull, Shape.Box, new Vector3(0f, 2.05f, -0.9f), Vector3.zero, new Vector3(3.8f, 0.2f, 3.8f), mPrimary);
            // Engine deck with exhaust stacks
            Vent(hull, new Vector3(0f, 2.02f, -3.6f), new Vector3(-90f, 0f, 0f), 3.4f, 1.6f, 5);
            for (int s = -1; s <= 1; s += 2)
            {
                P(hull, Shape.Cylinder, new Vector3(s * 2.0f, 2.8f, -4.0f), Vector3.zero, new Vector3(0.6f, 1.6f, 0.6f), mDark);
                P(hull, Shape.Cylinder, new Vector3(s * 2.0f, 3.62f, -4.0f), Vector3.zero, new Vector3(0.48f, 0.08f, 0.48f), mHot);
                Lamp(hull, new Vector3(s * 1.9f, 1.4f, 5.9f), new Vector3(-30f, 0f, 0f), 0.45f, A.Glow(Color.white, 3f));
                P(hull, Shape.Box, new Vector3(s * 1.2f, 2.02f, 2.4f), Vector3.zero, new Vector3(0.25f, 0.05f, 3.0f), mAccent);
            }
            Rivets(hull, new Vector3(-2.0f, 2.02f, 1.2f), new Vector3(0.8f, 0f, 0f), 6, 0.14f);
            var nozzles = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
                nozzles.Add(Booster(t, hull, new Vector3(s * 1.3f, 1.2f, -4.9f), 1.2f));

            var wheels = new List<WheeledLocomotion.Wheel>();
            float[] wz = { 4.6f, 1.6f, -1.4f, -4.4f };
            foreach (float z in wz)
                for (int s = -1; s <= 1; s += 2)
                {
                    wheels.Add(BuildWheel(t, s, new Vector3(s * 3.4f, 0.2f, z), 1.5f, 1.3f));
                    WheelStrut(hull, s, new Vector3(s * 3.4f, 0.2f, z), 1.0f);
                }
            // Armoured skirts (blow off)
            var skirts = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
                for (int seg = 0; seg < 2; seg++)
                {
                    var sk = Group(t, "Skirt", new Vector3(s * 3.45f, 2.05f, seg == 0 ? 3.1f : -2.9f));
                    Armor(sk, Vector3.zero, new Vector3(-90f, 0f, -s * 8f), new Vector3(1.9f, 5.6f, 0.3f), mPrimary);
                    Armor(sk, new Vector3(s * 0.95f, -0.45f, 0f), new Vector3(0f, s * 90f, 0f), new Vector3(5.4f, 0.9f, 0.22f), mPrimary, mDark, false);
                    P(sk, Shape.Box, new Vector3(s * 1.08f, -0.3f, 0f), Vector3.zero, new Vector3(0.05f, 0.2f, 4.8f), mAccent);
                    MeshMerge.Merge(sk);
                    skirts.Add(sk);
                }

            var u = WheeledUpper(t, waist, k, 0, mSecondary);
            Pedestal(hull, waist, 2.05f, k);
            // Heavy collar armour on the big torso
            Armor(u.torso, new Vector3(0f, 3.25f, 1.25f) * k, new Vector3(-30f, 0f, 0f), new Vector3(3.0f, 0.8f, 0.3f) * k, mPrimary, mDark, false);
            MeshMerge.Merge(hull);
            MergeUpper(u);

            var parts = new List<Transform> { skirts[0], u.plates[0], skirts[2], u.antenna, skirts[1], u.plates[1], skirts[3] };
            AddDamageable(root, name, team, 3100f, 2.6f, u.smoke, parts, 0.35f);

            var drive = root.AddComponent<WheeledLocomotion>();
            drive.wheels = wheels.ToArray();
            root.GetComponent<Rigidbody>().centerOfMass = new Vector3(0f, 0.3f, 0.3f);
            drive.extraAirGravity = 0.7f;
            drive.thrusterNozzles = nozzles.ToArray();
            drive.wheelRadius = 1.5f;
            drive.suspensionTravel = 1.2f;
            drive.driveAccel = 7f;
            drive.maxSpeed = 19f;
            drive.turnRate = 0.95f;
            drive.boostAccel = 20f;
            drive.boostMaxSpeed = 46f;

            var turret = AddUpperTurret(root, u, player, 60f, 35f, 140f);
            return UpperChassis(root, turret, u, k);
        }

        // ---------------------------------------------------------------------
        // BASTION: six-wheeled siege robot with a heavy dozer shield up front
        // and a visor-helmeted torso.
        // ---------------------------------------------------------------------
        static Chassis BuildBastion(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            const float k = 0.95f;
            var root = NewRoot(name, groundPos + Vector3.up * 2.8f, rotation, 38000f, false);
            var t = root.transform;
            var waist = new Vector3(0f, 2.7f, -1.0f);
            AddBox(t, new Vector3(0f, 1.0f, 0f), new Vector3(6.0f, 2.4f, 11.6f));
            AddBox(t, waist + new Vector3(0f, 2.3f, -0.3f) * k, new Vector3(4.4f, 4.4f, 3.6f) * k);

            var hull = Group(t, "HullStatic", Vector3.zero);
            P(hull, Shape.Box, new Vector3(0f, 0.3f, 0f), Vector3.zero, new Vector3(3.8f, 0.9f, 9.6f), mDark);
            P(hull, Shape.Box, new Vector3(0f, 1.2f, -0.6f), Vector3.zero, new Vector3(4.8f, 1.4f, 7.6f), mSecondary);
            P(hull, Shape.Wedge, new Vector3(0f, 1.15f, 4.1f), Vector3.zero, new Vector3(4.6f, 1.3f, 1.8f), mPrimary);
            P(hull, Shape.Box, new Vector3(0f, 1.92f, -1.0f), Vector3.zero, new Vector3(3.6f, 0.2f, 3.6f), mPrimary);
            // Dozer shield on two arms
            var shield = Group(t, "Shield", new Vector3(0f, 1.2f, 5.6f));
            shield.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            Armor(shield, Vector3.zero, Vector3.zero, new Vector3(6.0f, 2.2f, 0.5f), mPrimary);
            P(shield, Shape.Box, new Vector3(0f, -1.05f, 0.1f), Vector3.zero, new Vector3(6.2f, 0.3f, 0.7f), mMetal);
            for (int s = -1; s <= 1; s += 2)
            {
                Armor(shield, new Vector3(s * 3.05f, 0f, -0.4f), new Vector3(0f, s * 70f, 0f), new Vector3(1.0f, 2.0f, 0.4f), mPrimary, mDark, false);
                P(shield, Shape.Box, new Vector3(s * 1.4f, 0.75f, 0.32f), Vector3.zero, new Vector3(1.2f, 0.14f, 0.05f), mAccent);
            }
            MeshMerge.Merge(shield);
            for (int s = -1; s <= 1; s += 2)
            {
                Piston(hull, new Vector3(s * 1.6f, 1.0f, 3.2f), new Vector3(s * 1.6f, 1.0f, 5.3f), 0.2f);
                Armor(hull, new Vector3(s * 2.45f, 1.2f, -0.6f), new Vector3(0f, s * 90f, 0f), new Vector3(6.6f, 1.1f, 0.3f), mPrimary);
                Lamp(hull, new Vector3(s * 1.7f, 1.6f, 4.6f), new Vector3(-20f, 0f, 0f), 0.4f, A.Glow(Color.white, 3f));
                P(hull, Shape.Box, new Vector3(s * 1.0f, 1.92f, 2.0f), Vector3.zero, new Vector3(0.25f, 0.05f, 2.4f), mAccent);
            }
            Vent(hull, new Vector3(0f, 1.25f, -4.42f), new Vector3(0f, 180f, 0f), 3.0f, 0.9f, 4);
            var nozzles = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
                nozzles.Add(Booster(t, hull, new Vector3(s * 1.5f, 1.1f, -4.5f), 1.1f));

            var fenders = new List<Transform>();
            var wheels = new List<WheeledLocomotion.Wheel>();
            float[] wz = { 3.6f, 0f, -3.6f };
            foreach (float z in wz)
                for (int s = -1; s <= 1; s += 2)
                {
                    wheels.Add(BuildWheel(t, s, new Vector3(s * 2.9f, 0.2f, z), 1.35f, 1.2f));
                    WheelStrut(hull, s, new Vector3(s * 2.9f, 0.2f, z), 1.0f);
                    if (z == 0f) continue;
                    var f = Group(t, "Fender", new Vector3(s * 2.9f, 1.85f, z));
                    Armor(f, Vector3.zero, new Vector3(-90f, 0f, -s * 10f), new Vector3(1.5f, 2.8f, 0.24f), mPrimary, mDark, false);
                    MeshMerge.Merge(f);
                    fenders.Add(f);
                }

            var u = WheeledUpper(t, waist, k, 1, mSecondary);
            Pedestal(hull, waist, 1.92f, k);
            MeshMerge.Merge(hull);
            MergeUpper(u);

            var parts = new List<Transform> { fenders[0], u.plates[0], shield, fenders[3], u.antenna, u.plates[1], fenders[1], fenders[2] };
            AddDamageable(root, name, team, 2800f, 2.1f, u.smoke, parts, 0.5f);

            var drive = root.AddComponent<WheeledLocomotion>();
            drive.wheels = wheels.ToArray();
            root.GetComponent<Rigidbody>().centerOfMass = new Vector3(0f, 0.3f, 0f);
            drive.extraAirGravity = 0.6f;
            drive.thrusterNozzles = nozzles.ToArray();
            drive.wheelRadius = 1.35f;
            drive.suspensionTravel = 0.9f;
            drive.driveAccel = 8f;
            drive.maxSpeed = 22f;
            drive.turnRate = 1.0f;
            drive.lateralGrip = 9f;
            drive.boostAccel = 18f;
            drive.boostMaxSpeed = 42f;

            var turret = AddUpperTurret(root, u, player, 55f, 30f, 180f);
            return UpperChassis(root, turret, u, k);
        }

        // =====================================================================
        // Shared detail helpers
        // =====================================================================

        /// <summary>A detailed wheel: tyre with tread blocks, rim, spokes and hub.</summary>
        static WheeledLocomotion.Wheel BuildWheel(Transform t, float side, Vector3 localPos, float radius, float width)
        {
            var anchor = Group(t, "WheelAnchor", localPos + Vector3.up * 1.0f);
            var vis = Group(t, "Wheel", localPos);
            var ws = Group(vis, "WheelStatic", Vector3.zero);
            float d = radius * 2f;
            P(ws, Shape.Cylinder, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(d, width, d), mRubber);
            P(ws, Shape.Cylinder, new Vector3(side * width * 0.52f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(d * 0.62f, 0.1f, d * 0.62f), mMetal);
            P(ws, Shape.Cylinder, new Vector3(side * width * 0.58f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(d * 0.25f, 0.12f, d * 0.25f), mAccent);
            for (int k = 0; k < 6; k++)
                P(ws, Shape.Box, new Vector3(side * width * 0.55f, 0f, 0f), new Vector3(k * 60f, 0f, 0f), new Vector3(0.08f, d * 0.55f, 0.18f), mDark);
            for (int k = 0; k < 6; k++)
            {
                float a = (k * 60f + 30f) * Mathf.Deg2Rad;
                P(ws, Shape.Cylinder, new Vector3(side * width * 0.56f, Mathf.Cos(a) * radius * 0.42f, Mathf.Sin(a) * radius * 0.42f),
                    new Vector3(0f, 0f, 90f), new Vector3(0.14f, 0.06f, 0.14f), mMetal);
            }
            for (int k = 0; k < 14; k++)
            {
                float a = k * (360f / 14f) * Mathf.Deg2Rad;
                P(ws, Shape.Box, new Vector3(0f, Mathf.Cos(a) * radius * 0.98f, Mathf.Sin(a) * radius * 0.98f), new Vector3(k * (360f / 14f), 0f, 0f), new Vector3(width * 1.04f, 0.14f, radius * 0.22f), mRubber);
            }
            MeshMerge.Merge(ws);
            return new WheeledLocomotion.Wheel { anchor = anchor, visual = vis };
        }

        /// <summary>A row of bolt heads: tiny detail that makes plates read as armour.</summary>
        static void Rivets(Transform parent, Vector3 start, Vector3 step, int count, float size)
        {
            for (int i = 0; i < count; i++)
                P(parent, Shape.Sphere, start + step * i, Vector3.zero, new Vector3(size, size * 0.6f, size), mMetal);
        }
    }
}
