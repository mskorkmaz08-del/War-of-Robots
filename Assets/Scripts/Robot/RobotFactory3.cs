using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public static partial class RobotFactory
    {
        // =====================================================================
        // LANCER: tall, slim sniper walker on reverse-jointed legs
        // =====================================================================

        static GameObject BuildLancer(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            const float stand = 7.4f;
            const float thigh = 4.4f;
            const float shin = 4.8f;

            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(groundPos + Vector3.up * stand, rotation);
            var t = root.transform;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 32000f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            AddBox(t, new Vector3(0f, 2.4f, 0.2f), new Vector3(4.6f, 4.6f, 5.6f));
            AddBox(t, new Vector3(0f, -3.4f, 0f), new Vector3(3.6f, 5f, 3f));

            // ---- Pelvis ----
            var hull = Group(t, "HullStatic", Vector3.zero);
            P(hull, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(2.8f, 1.2f, 2.4f), mDark);
            P(hull, Shape.Hex, new Vector3(0f, 0.6f, 0f), Vector3.zero, new Vector3(2.4f, 0.5f, 2.4f), mSecondary);
            for (int s = -1; s <= 1; s += 2)
            {
                P(hull, Shape.Sphere, new Vector3(s * 1.75f, -0.2f, 0f), Vector3.zero, Vector3.one * 1.5f, mDark);
                P(hull, Shape.Cylinder, new Vector3(s * 2.05f, -0.2f, 0f), new Vector3(0f, 0f, 90f), new Vector3(1.1f, 0.6f, 1.1f), mMetal);
            }

            // ---- Reverse-jointed legs ----
            var legs = new List<WalkerLocomotion.Leg>();
            for (int s = -1; s <= 1; s += 2)
            {
                var hip = Group(t, s < 0 ? "HipL" : "HipR", new Vector3(s * 2.1f, -0.2f, 0f));

                var thighT = Group(t, "Thigh", Vector3.zero);
                P(thighT, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(1.0f, 1.3f, thigh * 0.9f), mSecondary);
                P(thighT, Shape.Box, new Vector3(0f, 0.75f, 0f), Vector3.zero, new Vector3(1.3f, 0.25f, thigh * 0.7f), mPrimary);
                P(thighT, Shape.Cylinder, new Vector3(s * 0.7f, -0.3f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.35f, thigh * 0.8f, 0.35f), mMetal);
                P(thighT, Shape.Box, new Vector3(0f, 0.9f, 0.6f), Vector3.zero, new Vector3(0.35f, 0.08f, thigh * 0.35f), mAccent);

                var kneeT = Group(t, "Knee", Vector3.zero);
                P(kneeT, Shape.Sphere, Vector3.zero, Vector3.zero, Vector3.one * 1.4f, mDark);
                P(kneeT, Shape.Cylinder, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(1.0f, 0.8f, 1.0f), mMetal);

                var shinT = Group(t, "Shin", Vector3.zero);
                P(shinT, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(0.9f, 1.1f, shin * 0.92f), mSecondary);
                P(shinT, Shape.Box, new Vector3(0f, 0.65f, 0f), Vector3.zero, new Vector3(1.2f, 0.25f, shin * 0.75f), mPrimary);
                P(shinT, Shape.Cylinder, new Vector3(0f, -0.65f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.38f, shin * 0.85f, 0.38f), mMetal);
                P(shinT, Shape.Box, new Vector3(0f, 0.82f, shin * 0.2f), Vector3.zero, new Vector3(0.8f, 0.06f, 0.2f), mAccent);

                var footT = Group(t, "Foot", Vector3.zero);
                // Three splayed toes and a heel spur.
                for (int k = -1; k <= 1; k++)
                    P(footT, Shape.Wedge, new Vector3(k * 0.7f, 0f, 1.6f), new Vector3(0f, k * 18f, 0f), new Vector3(0.6f, 0.6f, 2.4f), mDark);
                P(footT, Shape.Box, new Vector3(0f, 0.3f, 0.3f), Vector3.zero, new Vector3(1.6f, 0.7f, 1.6f), mSecondary);
                P(footT, Shape.Wedge, new Vector3(0f, 0f, -1.1f), new Vector3(0f, 180f, 0f), new Vector3(0.6f, 0.5f, 1.2f), mDark);
                P(footT, Shape.Sphere, new Vector3(0f, 0.75f, 0.2f), Vector3.zero, Vector3.one * 1.0f, mMetal);

                legs.Add(new WalkerLocomotion.Leg
                {
                    hip = hip, thigh = thighT, knee = kneeT, shin = shinT, foot = footT, side = s,
                    bendHint = new Vector3(0f, 0.3f, -1f)
                });
            }

            // ---- Upper body ----
            var upper = Group(t, "Upper", new Vector3(0f, 1.0f, 0f));
            var torso = Group(upper, "TorsoStatic", Vector3.zero);
            P(torso, Shape.Cylinder, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(2.4f, 0.4f, 2.4f), mDark);
            P(torso, Shape.Box, new Vector3(0f, 1.6f, -0.3f), Vector3.zero, new Vector3(3.6f, 2.4f, 3.6f), mSecondary);
            P(torso, Shape.Wedge, new Vector3(0f, 1.5f, 2.2f), new Vector3(0f, 0f, 0f), new Vector3(3.0f, 2.0f, 1.6f), mPrimary);
            P(torso, Shape.Box, new Vector3(0f, 2.95f, -0.2f), Vector3.zero, new Vector3(3.2f, 0.3f, 3.2f), mPrimary);
            // Long sensor head
            P(torso, Shape.Box, new Vector3(0f, 3.4f, 1.2f), Vector3.zero, new Vector3(1.4f, 0.8f, 2.4f), mSecondary);
            P(torso, Shape.Box, new Vector3(0f, 3.45f, 2.45f), Vector3.zero, new Vector3(1.1f, 0.25f, 0.1f), mGlow);
            P(torso, Shape.Cylinder, new Vector3(0.45f, 3.85f, 1.6f), new Vector3(90f, 0f, 0f), new Vector3(0.35f, 0.5f, 0.35f), mBlack);
            P(torso, Shape.Cylinder, new Vector3(0.45f, 3.85f, 2.12f), new Vector3(90f, 0f, 0f), new Vector3(0.25f, 0.02f, 0.25f), mGlow);
            // Back radiators and an aerial
            for (int k = 0; k < 5; k++)
                P(torso, Shape.Box, new Vector3(0f, 0.9f + k * 0.38f, -2.15f), Vector3.zero, new Vector3(3.0f, 0.1f, 0.25f), mBlack);
            P(torso, Shape.Cylinder, new Vector3(-1.2f, 4.4f, -1.4f), Vector3.zero, new Vector3(0.1f, 2.6f, 0.1f), mMetal);
            P(torso, Shape.Sphere, new Vector3(-1.2f, 5.75f, -1.4f), Vector3.zero, Vector3.one * 0.25f, mGlow);
            Rivets(torso, new Vector3(-1.4f, 2.85f, 1.42f), new Vector3(0.7f, 0f, 0f), 5, 0.12f);

            // Shoulder plates (blow off)
            var plates = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var pd = Group(upper, s < 0 ? "ShoulderL" : "ShoulderR", new Vector3(s * 2.4f, 2.5f, 0f));
                pd.localRotation = Quaternion.Euler(0f, 0f, -s * 15f);
                P(pd, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(1.4f, 1.0f, 2.8f), mPrimary);
                P(pd, Shape.Box, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(1.0f, 0.08f, 2.4f), mAccent);
                MeshMerge.Merge(pd);
                plates.Add(pd);
            }

            // Missile box on the left shoulder.
            var tubes = new List<Transform>();
            var pod = Group(upper, "Pod", new Vector3(-2.5f, 3.6f, -0.6f));
            var podStatic = Group(pod, "PodStatic", Vector3.zero);
            P(podStatic, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(1.6f, 1.2f, 2.2f), mSecondary);
            for (int r = 0; r < 2; r++)
            for (int c = 0; c < 2; c++)
            {
                Vector3 tp = new Vector3((c - 0.5f) * 0.6f, 0.62f, 0.4f - r * 0.8f);
                P(podStatic, Shape.Cylinder, tp, Vector3.zero, new Vector3(0.4f, 0.06f, 0.4f), mBlack);
                var tube = Group(pod, "Tube", tp + Vector3.up * 0.1f);
                tube.localRotation = Quaternion.Euler(-70f, 0f, 0f);
                tubes.Add(tube);
            }
            MeshMerge.Merge(podStatic);

            // Long rail rifle under the right arm (pitches to aim).
            var gun = Group(upper, "Rifle", new Vector3(2.4f, 1.4f, 0.4f));
            var gs = Group(gun, "RifleStatic", Vector3.zero);
            P(gs, Shape.Box, new Vector3(0f, 0f, 0.4f), Vector3.zero, new Vector3(1.1f, 1.2f, 3.2f), mPrimary);
            P(gs, Shape.Box, new Vector3(0f, 0.7f, 0.2f), Vector3.zero, new Vector3(0.6f, 0.3f, 2.4f), mDark);
            P(gs, Shape.Cylinder, new Vector3(0f, 0.95f, 0.6f), new Vector3(90f, 0f, 0f), new Vector3(0.35f, 1.2f, 0.35f), mBlack);
            for (int s = -1; s <= 1; s += 2)
                P(gs, Shape.Box, new Vector3(s * 0.28f, 0f, 5.0f), Vector3.zero, new Vector3(0.22f, 0.5f, 7.0f), mDark);
            for (int k = 0; k < 5; k++)
                P(gs, Shape.Box, new Vector3(0f, 0f, 2.6f + k * 1.2f), Vector3.zero, new Vector3(0.7f, 0.15f, 0.25f), mGlow);
            P(gs, Shape.Box, new Vector3(0f, 0f, 8.6f), Vector3.zero, new Vector3(0.9f, 0.7f, 0.5f), mMetal);
            MeshMerge.Merge(gs);
            var muzzle = Group(gun, "Muzzle", new Vector3(0f, 0f, 9.0f));

            var jets = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var jet = Group(upper, "Jet", new Vector3(s * 1.4f, 0.4f, -2.2f));
                jet.localRotation = Quaternion.LookRotation(new Vector3(0f, -1f, -0.3f));
                jets.Add(jet);
            }
            var smoke = Group(upper, "SmokePoint", new Vector3(0f, 2.6f, -1f));

            MeshMerge.Merge(hull);
            MeshMerge.Merge(torso);
            foreach (var leg in legs)
            {
                MeshMerge.Merge(leg.thigh);
                MeshMerge.Merge(leg.knee);
                MeshMerge.Merge(leg.shin);
                MeshMerge.Merge(leg.foot);
            }

            root.AddComponent<RobotCommands>();
            var dmg = root.AddComponent<Damageable>();
            dmg.displayName = name;
            dmg.team = team;
            dmg.maxHealth = 1700f;
            dmg.size = 1.8f;
            dmg.smokePoint = smoke;
            dmg.detachableParts = new List<Transform> { plates[0], pod, plates[1] };
            ResetHealth(dmg);

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
            turret.pitchPivot = gun;
            turret.yawSpeed = 70f;
            turret.pitchSpeed = 40f;
            turret.maxYaw = 120f;
            turret.acquireCone = player ? 70f : 180f;

            var rail = root.AddComponent<PlasmaRailgun>();
            rail.displayName = "Rail Rifle";
            rail.slot = Weapon.Slot.Primary;
            rail.muzzles = new[] { muzzle };
            rail.cooldown = 2.2f;
            rail.chargeTime = 0.7f;
            rail.damage = 240f;
            rail.color = new Color(0.6f, 0.9f, 1f);

            var missiles = root.AddComponent<MissileSwarm>();
            missiles.displayName = "Missile Box";
            missiles.slot = Weapon.Slot.Special;
            missiles.muzzles = tubes.ToArray();
            missiles.cooldown = 7f;
            missiles.missilesPerSalvo = 8;

            return root;
        }

        // =====================================================================
        // BASTION: six-wheeled artillery tank with a long cannon and mortars
        // =====================================================================

        static GameObject BuildBastion(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(groundPos + Vector3.up * 2.8f, rotation);
            var t = root.transform;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 38000f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            AddBox(t, new Vector3(0f, 1.1f, 0f), new Vector3(6.0f, 2.6f, 11.6f));
            AddBox(t, new Vector3(0f, 3.1f, -1.0f), new Vector3(4.6f, 1.8f, 5.4f));

            var hull = Group(t, "HullStatic", Vector3.zero);
            P(hull, Shape.Box, new Vector3(0f, 1.1f, 0f), Vector3.zero, new Vector3(5.2f, 1.8f, 10.2f), mPrimary);
            P(hull, Shape.Box, new Vector3(0f, 0.3f, 0f), Vector3.zero, new Vector3(4.4f, 0.9f, 10.6f), mDark);
            P(hull, Shape.Wedge, new Vector3(0f, 1.1f, 5.7f), Vector3.zero, new Vector3(5.2f, 1.8f, 1.6f), mPrimary);
            P(hull, Shape.Box, new Vector3(0f, 2.05f, 0.5f), Vector3.zero, new Vector3(4.6f, 0.15f, 8.4f), mSecondary);
            // Track skirts and stowage
            for (int s = -1; s <= 1; s += 2)
            {
                P(hull, Shape.Box, new Vector3(s * 2.75f, 1.7f, 0f), Vector3.zero, new Vector3(0.3f, 0.9f, 10.4f), mSecondary);
                P(hull, Shape.Box, new Vector3(s * 2.95f, 1.9f, 0f), Vector3.zero, new Vector3(0.1f, 0.12f, 9.6f), mAccent);
                for (int k = 0; k < 3; k++)
                    P(hull, Shape.Cylinder, new Vector3(s * 2.2f, 2.35f, -3.6f + k * 0.9f), new Vector3(0f, 0f, 90f), new Vector3(0.7f, 0.8f, 0.7f), mDark);
                P(hull, Shape.Box, new Vector3(s * 1.6f, 1.4f, 5.9f), Vector3.zero, new Vector3(0.6f, 0.25f, 0.1f), A.Glow(Color.white, 3f));
            }
            // Rear engine deck
            for (int k = 0; k < 6; k++)
                P(hull, Shape.Box, new Vector3(0f, 2.15f, -3.2f - k * 0.3f), Vector3.zero, new Vector3(3.6f, 0.08f, 0.15f), mBlack);
            P(hull, Shape.Cylinder, new Vector3(-1.8f, 2.6f, -4.6f), Vector3.zero, new Vector3(0.5f, 1.2f, 0.5f), mDark);
            Rivets(hull, new Vector3(-2.0f, 2.14f, 4.2f), new Vector3(0.5f, 0f, 0f), 9, 0.12f);

            var plates = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var f = Group(t, "SidePlate", new Vector3(s * 3.0f, 1.3f, 2.4f));
                P(f, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(0.25f, 1.2f, 3.4f), mPrimary);
                P(f, Shape.Box, new Vector3(s * 0.14f, 0.3f, 0f), Vector3.zero, new Vector3(0.05f, 0.12f, 3.0f), mAccent);
                MeshMerge.Merge(f);
                plates.Add(f);
            }

            var wheels = new List<WheeledLocomotion.Wheel>();
            foreach (float z in new[] { 3.6f, 0f, -3.6f })
                for (int s = -1; s <= 1; s += 2)
                    wheels.Add(BuildWheel(t, s, new Vector3(s * 2.6f, 0.2f, z), 1.35f, 1.2f));

            // Turret: heavy box turret with a long gun and a mortar rack behind.
            var turretT = Group(t, "Turret", new Vector3(0f, 2.3f, -0.6f));
            var ts = Group(turretT, "TurretStatic", Vector3.zero);
            P(ts, Shape.Cylinder, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(3.6f, 0.3f, 3.6f), mDark);
            P(ts, Shape.Box, new Vector3(0f, 0.9f, 0.2f), Vector3.zero, new Vector3(3.6f, 1.4f, 4.0f), mSecondary);
            P(ts, Shape.Wedge, new Vector3(0f, 0.9f, 2.6f), Vector3.zero, new Vector3(3.6f, 1.4f, 1.0f), mSecondary);
            P(ts, Shape.Box, new Vector3(0f, 1.65f, 0.2f), Vector3.zero, new Vector3(3.0f, 0.12f, 3.4f), mPrimary);
            P(ts, Shape.Box, new Vector3(1.0f, 1.85f, -0.4f), Vector3.zero, new Vector3(0.9f, 0.45f, 0.9f), mDark);
            P(ts, Shape.Box, new Vector3(1.0f, 1.9f, 0.08f), Vector3.zero, new Vector3(0.6f, 0.15f, 0.05f), mGlow);
            var mortarTubes = new List<Transform>();
            for (int k = 0; k < 3; k++)
            {
                Vector3 mp = new Vector3((k - 1) * 0.8f, 1.6f, -1.7f);
                P(ts, Shape.Cylinder, mp, new Vector3(-20f, 0f, 0f), new Vector3(0.55f, 1.4f, 0.55f), mDark);
                var mt = Group(turretT, "MortarTube", mp + new Vector3(0f, 0.7f, 0.25f));
                mt.localRotation = Quaternion.Euler(-70f, 0f, 0f);
                mortarTubes.Add(mt);
            }

            var gun = Group(turretT, "Gun", new Vector3(0f, 0.9f, 2.9f));
            var gs = Group(gun, "GunStatic", Vector3.zero);
            P(gs, Shape.Box, new Vector3(0f, 0f, 0.2f), Vector3.zero, new Vector3(1.2f, 1.0f, 1.2f), mDark);
            P(gs, Shape.Cylinder, new Vector3(0f, 0f, 3.6f), new Vector3(90f, 0f, 0f), new Vector3(0.6f, 3.6f, 0.6f), mMetal);
            P(gs, Shape.Cylinder, new Vector3(0f, 0f, 2.0f), new Vector3(90f, 0f, 0f), new Vector3(0.85f, 0.6f, 0.85f), mDark);
            P(gs, Shape.Box, new Vector3(0f, 0f, 7.2f), Vector3.zero, new Vector3(0.95f, 0.6f, 0.9f), mDark);
            P(gs, Shape.Box, new Vector3(0f, 0f, 7.2f), Vector3.zero, new Vector3(1.05f, 0.15f, 0.5f), mBlack);
            MeshMerge.Merge(gs);
            var muzzle = Group(gun, "Muzzle", new Vector3(0f, 0f, 7.8f));

            var nozzles = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var noz = Group(t, "Booster", new Vector3(s * 1.2f, 1.2f, -5.3f));
                noz.localRotation = Quaternion.LookRotation(Vector3.back);
                nozzles.Add(noz);
            }

            var smoke = Group(t, "SmokePoint", new Vector3(0f, 2.4f, -3.6f));
            MeshMerge.Merge(hull);
            MeshMerge.Merge(ts);

            root.AddComponent<RobotCommands>();
            var dmg = root.AddComponent<Damageable>();
            dmg.displayName = name;
            dmg.team = team;
            dmg.maxHealth = 2800f;
            dmg.size = 2.1f;
            dmg.smokePoint = smoke;
            dmg.detachableParts = new List<Transform> { plates[0], plates[1] };
            dmg.frontArmor = 0.5f;
            ResetHealth(dmg);

            var drive = root.AddComponent<WheeledLocomotion>();
            drive.wheels = wheels.ToArray();
            rb.centerOfMass = new Vector3(0f, 0.3f, 0f);
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

            var turret = root.AddComponent<Turret>();
            turret.yawPivot = turretT;
            turret.pitchPivot = gun;
            turret.yawSpeed = 50f;
            turret.pitchSpeed = 30f;
            turret.maxYaw = 180f;
            turret.projectileSpeed = 140f;
            turret.compensateGravity = true;
            turret.acquireCone = player ? 75f : 180f;

            var cannon = root.AddComponent<HeavyCannon>();
            cannon.displayName = "Long Cannon";
            cannon.slot = Weapon.Slot.Primary;
            cannon.muzzles = new[] { muzzle };
            cannon.cooldown = player ? 1.2f : 1.6f;
            cannon.shellSpeed = 140f;
            cannon.damage = 160f;
            cannon.blastRadius = 8f;
            cannon.color = new Color(1f, 0.6f, 0.2f);

            var mortar = root.AddComponent<MortarBarrage>();
            mortar.displayName = "Mortar Barrage";
            mortar.slot = Weapon.Slot.Special;
            mortar.muzzles = mortarTubes.ToArray();
            mortar.cooldown = 9f;
            mortar.shells = 6;

            return root;
        }
    }
}
