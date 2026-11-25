using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// The newer machines: ARACHNE (four-legged spider walker), HORNET (light, fast
    /// four-wheeler) and JUGGERNAUT (eight-wheeled heavy flame tank).
    /// </summary>
    public static partial class RobotFactory
    {
        // =====================================================================
        // ARACHNE: four-legged spider walker with gatling guns and a mortar
        // =====================================================================

        static GameObject BuildArachne(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            const float stand = 6.5f;
            const float thigh = 4.6f;
            const float shin = 5.6f;

            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(groundPos + Vector3.up * stand, rotation);
            var t = root.transform;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 36000f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            AddBox(t, new Vector3(0f, 0.6f, -0.6f), new Vector3(6f, 3.2f, 8f));

            // ---- Body: armoured thorax and a big abdomen at the back ----
            var body = Group(t, "BodyStatic", Vector3.zero);
            P(body, Shape.Box, new Vector3(0f, 0.2f, 0.6f), Vector3.zero, new Vector3(4.8f, 1.8f, 4.4f), mSecondary);
            P(body, Shape.Hex, new Vector3(0f, 0.2f, 0.6f), Vector3.zero, new Vector3(5.6f, 1.2f, 5.0f), mDark);
            P(body, Shape.Box, new Vector3(0f, 1.25f, 0.6f), Vector3.zero, new Vector3(3.8f, 0.4f, 3.6f), mPrimary);
            P(body, Shape.Wedge, new Vector3(0f, 0.3f, 3.2f), Vector3.zero, new Vector3(4.2f, 1.4f, 1.2f), mPrimary);
            P(body, Shape.Sphere, new Vector3(0f, 0.6f, -3.0f), Vector3.zero, new Vector3(5.2f, 3.4f, 5.6f), mDark);
            P(body, Shape.Sphere, new Vector3(0f, 1.1f, -3.0f), Vector3.zero, new Vector3(4.4f, 2.8f, 4.8f), mPrimary);
            for (int k = 0; k < 4; k++)
                P(body, Shape.Box, new Vector3(0f, 2.35f - k * 0.05f, -1.6f - k * 1.0f), new Vector3(-10f + k * 8f, 0f, 0f), new Vector3(3.2f - k * 0.3f, 0.18f, 0.7f), mSecondary);
            for (int s = -1; s <= 1; s += 2)
            {
                P(body, Shape.Box, new Vector3(s * 2.0f, 1.0f, -3.0f), new Vector3(0f, 0f, s * 30f), new Vector3(0.15f, 1.6f, 3.6f), mAccent);
                P(body, Shape.Cylinder, new Vector3(s * 2.5f, -0.2f, 0.6f), new Vector3(0f, 0f, 90f), new Vector3(1.2f, 0.6f, 1.2f), mMetal);
                // Hydraulic lines running along the sides
                P(body, Shape.Cylinder, new Vector3(s * 2.45f, 0.6f, -0.8f), new Vector3(90f, 0f, 0f), new Vector3(0.22f, 3.6f, 0.22f), mBlack);
                P(body, Shape.Cylinder, new Vector3(s * 2.55f, 0.35f, -0.8f), new Vector3(90f, 0f, 0f), new Vector3(0.18f, 3.2f, 0.18f), mMetal);
            }
            // Cluster of eyes on the front
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 3 - 1) * 0.7f;
                float y = 0.5f + (i / 3) * 0.55f;
                P(body, Shape.Sphere, new Vector3(x, y, 3.05f), Vector3.zero, Vector3.one * (i / 3 == 0 ? 0.45f : 0.32f), mGlow);
            }
            P(body, Shape.Box, new Vector3(0f, 0.75f, 2.95f), Vector3.zero, new Vector3(2.6f, 1.3f, 0.2f), mBlack);
            Rivets(body, new Vector3(-2.2f, 1.47f, -1.0f), new Vector3(1.1f, 0f, 0f), 5, 0.14f);
            Rivets(body, new Vector3(-2.2f, 1.47f, 2.2f), new Vector3(1.1f, 0f, 0f), 5, 0.14f);

            // ---- Four legs, knees high like a spider ----
            var legs = new List<WalkerLocomotion.Leg>();
            // Order front-left, back-right, front-right, back-left gives a steady diagonal gait.
            var corners = new[] { new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, -1f) };
            foreach (var c in corners)
            {
                float s = c.x;
                var hip = Group(t, "Hip", new Vector3(s * 2.6f, 0f, c.y * 1.8f - 0.4f));
                P(body, Shape.Sphere, new Vector3(s * 2.6f, 0f, c.y * 1.8f - 0.4f), Vector3.zero, Vector3.one * 1.5f, mMetal);

                var thighT = Group(t, "Thigh", Vector3.zero);
                P(thighT, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(1.0f, 1.1f, thigh * 0.95f), mSecondary);
                P(thighT, Shape.Box, new Vector3(0f, 0.65f, 0.2f), Vector3.zero, new Vector3(1.25f, 0.25f, thigh * 0.8f), mPrimary);
                P(thighT, Shape.Cylinder, new Vector3(0f, -0.6f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.32f, thigh * 0.8f, 0.32f), mMetal);
                P(thighT, Shape.Box, new Vector3(0f, 0.82f, thigh * 0.25f), Vector3.zero, new Vector3(0.6f, 0.08f, 0.5f), mAccent);

                var kneeT = Group(t, "Knee", Vector3.zero);
                P(kneeT, Shape.Sphere, Vector3.zero, Vector3.zero, Vector3.one * 1.4f, mDark);
                P(kneeT, Shape.Wedge, new Vector3(0f, 0.8f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0.8f, 1.4f, 0.8f), mPrimary);
                P(kneeT, Shape.Sphere, new Vector3(0f, 0.2f, 0.65f), Vector3.zero, Vector3.one * 0.3f, mGlow);

                var shinT = Group(t, "Shin", Vector3.zero);
                P(shinT, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(0.8f, 0.9f, shin * 0.95f), mDark);
                P(shinT, Shape.Wedge, new Vector3(0f, 0.55f, -0.2f), Vector3.zero, new Vector3(0.9f, 0.6f, shin * 0.7f), mPrimary);
                P(shinT, Shape.Cylinder, new Vector3(0.45f, 0f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.25f, shin * 0.7f, 0.25f), mMetal);
                P(shinT, Shape.Box, new Vector3(0f, -0.5f, shin * 0.3f), Vector3.zero, new Vector3(0.5f, 0.1f, 0.8f), mAccent);

                var footT = Group(t, "Foot", Vector3.zero);
                P(footT, Shape.Cylinder, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(1.6f, 0.4f, 1.6f), mDark);
                P(footT, Shape.Hex, new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(1.1f, 0.5f, 1.1f), mSecondary);
                for (int k = 0; k < 3; k++)
                    P(footT, Shape.Wedge, new Vector3(0f, 0.05f, 0f), new Vector3(0f, k * 120f, 0f), new Vector3(0.4f, 0.3f, 2.0f), mMetal);

                legs.Add(new WalkerLocomotion.Leg
                {
                    hip = hip, thigh = thighT, knee = kneeT, shin = shinT, foot = footT, side = s,
                    footOffset = new Vector3(s * 4.6f, 0f, c.y * 3.0f),
                    bendHint = new Vector3(s * 0.5f, 1f, c.y * 0.3f)
                });
            }

            // ---- Gun head on top (turns to aim) ----
            var head = Group(t, "Head", new Vector3(0f, 1.6f, 1.0f));
            var hs = Group(head, "HeadStatic", Vector3.zero);
            P(hs, Shape.Cylinder, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(2.8f, 0.3f, 2.8f), mDark);
            P(hs, Shape.Hex, new Vector3(0f, 0.7f, 0f), Vector3.zero, new Vector3(2.6f, 1.0f, 2.8f), mPrimary);
            P(hs, Shape.Box, new Vector3(0f, 1.25f, -0.3f), Vector3.zero, new Vector3(1.4f, 0.2f, 1.4f), mSecondary);
            P(hs, Shape.Cylinder, new Vector3(-0.8f, 1.9f, -0.6f), Vector3.zero, new Vector3(0.1f, 1.3f, 0.1f), mMetal);
            P(hs, Shape.Sphere, new Vector3(-0.8f, 2.6f, -0.6f), Vector3.zero, Vector3.one * 0.2f, mGlow);

            var guns = Group(head, "Guns", new Vector3(0f, 0.7f, 0.6f));
            var gs = Group(guns, "GunStatic", Vector3.zero);
            var gunMuzzles = new List<Transform>();
            var spinners = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                P(gs, Shape.Box, new Vector3(s * 1.7f, 0f, 0.4f), Vector3.zero, new Vector3(0.9f, 1.0f, 2.0f), mSecondary);
                P(gs, Shape.Box, new Vector3(s * 1.7f, 0.6f, 0.4f), Vector3.zero, new Vector3(0.6f, 0.2f, 1.6f), mAccent);
                P(gs, Shape.Cylinder, new Vector3(s * 1.7f, 0f, 1.6f), new Vector3(90f, 0f, 0f), new Vector3(0.9f, 0.3f, 0.9f), mDark);
                var spin = Group(guns, "Barrels", new Vector3(s * 1.7f, 0f, 1.7f));
                for (int k = 0; k < 6; k++)
                {
                    float a = k * 60f * Mathf.Deg2Rad;
                    P(spin, Shape.Cylinder, new Vector3(Mathf.Cos(a) * 0.26f, Mathf.Sin(a) * 0.26f, 1.5f), new Vector3(90f, 0f, 0f), new Vector3(0.16f, 1.5f, 0.16f), mBlack);
                }
                P(spin, Shape.Cylinder, new Vector3(0f, 0f, 2.6f), new Vector3(90f, 0f, 0f), new Vector3(0.75f, 0.12f, 0.75f), mMetal);
                P(spin, Shape.Cylinder, new Vector3(0f, 0f, 1.0f), new Vector3(90f, 0f, 0f), new Vector3(0.75f, 0.12f, 0.75f), mMetal);
                MeshMerge.Merge(spin);
                spinners.Add(spin);
                gunMuzzles.Add(Group(guns, "Muzzle", new Vector3(s * 1.7f, 0f, 4.8f)));
            }

            // ---- Mortar battery on the abdomen ----
            var mortarStatic = Group(t, "MortarStatic", new Vector3(0f, 2.6f, -3.0f));
            P(mortarStatic, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(2.6f, 0.6f, 1.8f), mDark);
            var mortarMuzzles = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                Vector3 tp = new Vector3((i - 1) * 0.8f, 0.7f, 0f);
                P(mortarStatic, Shape.Cylinder, tp, new Vector3(-20f, 0f, 0f), new Vector3(0.6f, 0.9f, 0.6f), mSecondary);
                P(mortarStatic, Shape.Cylinder, tp + new Vector3(0f, 0.85f, 0.3f), new Vector3(-20f, 0f, 0f), new Vector3(0.45f, 0.06f, 0.45f), mHot);
                var m = Group(t, "MortarMuzzle", new Vector3(tp.x, 2.6f + 1.6f, -3.0f + 0.4f));
                m.localRotation = Quaternion.Euler(-70f, 0f, 0f);
                mortarMuzzles.Add(m);
            }
            MeshMerge.Merge(mortarStatic);

            // ---- Detachable armour ----
            var plates = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var plate = Group(t, "SidePlate", new Vector3(s * 2.75f, 0.9f, 1.0f));
                plate.localRotation = Quaternion.Euler(0f, 0f, -s * 20f);
                P(plate, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(0.3f, 1.2f, 2.6f), mPrimary);
                P(plate, Shape.Box, new Vector3(s * 0.16f, 0.3f, 0f), Vector3.zero, new Vector3(0.05f, 0.15f, 2.2f), mAccent);
                MeshMerge.Merge(plate);
                plates.Add(plate);
            }
            var dome = Group(t, "DomePlate", new Vector3(0f, 2.75f, -4.4f));
            P(dome, Shape.Box, Vector3.zero, new Vector3(20f, 0f, 0f), new Vector3(2.6f, 0.25f, 1.6f), mPrimary);
            MeshMerge.Merge(dome);
            var mast = Group(t, "SensorMast", new Vector3(1.4f, 1.6f, -1.2f));
            P(mast, Shape.Cylinder, new Vector3(0f, 1.0f, 0f), Vector3.zero, new Vector3(0.15f, 2.0f, 0.15f), mMetal);
            P(mast, Shape.Box, new Vector3(0f, 2.0f, 0f), Vector3.zero, new Vector3(0.9f, 0.4f, 0.15f), mDark);
            P(mast, Shape.Sphere, new Vector3(0f, 2.3f, 0f), Vector3.zero, Vector3.one * 0.22f, mGlow);
            MeshMerge.Merge(mast);

            var jets = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var jet = Group(t, "Jet", new Vector3(s * 1.2f, -1.0f, -3.0f));
                jet.localRotation = Quaternion.LookRotation(Vector3.down);
                jets.Add(jet);
            }

            var smoke = Group(t, "SmokePoint", new Vector3(0f, 2.2f, -2.4f));

            MeshMerge.Merge(body);
            MeshMerge.Merge(hs);
            MeshMerge.Merge(gs);
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
            dmg.maxHealth = 2100f;
            dmg.size = 2.2f;
            dmg.smokePoint = smoke;
            dmg.detachableParts = new List<Transform> { plates[0], mast, dome, plates[1] };
            ResetHealth(dmg);

            var walker = root.AddComponent<WalkerLocomotion>();
            walker.legs = legs.ToArray();
            walker.jetNozzles = jets.ToArray();
            walker.standHeight = stand;
            walker.thighLength = thigh;
            walker.shinLength = shin;
            walker.strideLength = 4.6f;
            walker.stepDuration = 0.3f;
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

            var gat = root.AddComponent<Gatling>();
            gat.displayName = "Twin Gatlings";
            gat.slot = Weapon.Slot.Primary;
            gat.muzzles = gunMuzzles.ToArray();
            gat.spinners = spinners.ToArray();
            gat.cooldown = 0.07f;
            gat.color = new Color(1f, 0.85f, 0.4f);

            var mortar = root.AddComponent<MortarBarrage>();
            mortar.displayName = "Mortar Barrage";
            mortar.slot = Weapon.Slot.Special;
            mortar.muzzles = mortarMuzzles.ToArray();
            mortar.cooldown = 8f;

            return root;
        }

        // =====================================================================
        // HORNET: light, very fast four-wheeler with rockets and a tesla coil
        // =====================================================================

        static GameObject BuildHornet(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(groundPos + Vector3.up * 2.2f, rotation);
            var t = root.transform;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 18000f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            AddBox(t, new Vector3(0f, 0.8f, 0f), new Vector3(4.4f, 1.8f, 8.4f));

            var hull = Group(t, "HullStatic", Vector3.zero);
            P(hull, Shape.Box, new Vector3(0f, 0.8f, -0.4f), Vector3.zero, new Vector3(3.6f, 1.2f, 6.4f), mPrimary);
            P(hull, Shape.Box, new Vector3(0f, 0.2f, 0f), Vector3.zero, new Vector3(3.0f, 0.6f, 7.4f), mDark);
            P(hull, Shape.Wedge, new Vector3(0f, 0.8f, 3.6f), Vector3.zero, new Vector3(3.6f, 1.2f, 2.2f), mPrimary);
            P(hull, Shape.Wedge, new Vector3(0f, 0.35f, 4.4f), Vector3.zero, new Vector3(3.8f, 0.5f, 1.2f), mSecondary);
            // Canopy
            P(hull, Shape.Box, new Vector3(0f, 1.6f, 0.9f), Vector3.zero, new Vector3(1.8f, 0.6f, 2.0f), mSecondary);
            P(hull, Shape.Wedge, new Vector3(0f, 1.6f, 2.3f), Vector3.zero, new Vector3(1.8f, 0.6f, 0.8f), mSecondary);
            P(hull, Shape.Box, new Vector3(0f, 1.8f, 2.35f), new Vector3(-36f, 0f, 0f), new Vector3(1.5f, 0.4f, 0.05f), mGlow);
            // Side intakes and racing stripes
            for (int s = -1; s <= 1; s += 2)
            {
                P(hull, Shape.Box, new Vector3(s * 1.85f, 0.9f, 1.4f), Vector3.zero, new Vector3(0.3f, 0.8f, 1.6f), mBlack);
                for (int k = 0; k < 4; k++)
                    P(hull, Shape.Box, new Vector3(s * 1.95f, 0.65f + k * 0.18f, 1.4f), Vector3.zero, new Vector3(0.1f, 0.06f, 1.4f), mDark);
                P(hull, Shape.Box, new Vector3(s * 0.55f, 1.42f, -0.4f), Vector3.zero, new Vector3(0.25f, 0.04f, 6.0f), mAccent);
                P(hull, Shape.Box, new Vector3(s * 1.4f, 1.0f, 4.0f), Vector3.zero, new Vector3(0.5f, 0.2f, 0.1f), A.Glow(Color.white, 3f));
            }
            // Rear wing
            P(hull, Shape.Box, new Vector3(0f, 2.4f, -3.4f), new Vector3(-8f, 0f, 0f), new Vector3(4.2f, 0.12f, 1.0f), mPrimary);
            P(hull, Shape.Box, new Vector3(0f, 2.45f, -3.85f), new Vector3(-8f, 0f, 0f), new Vector3(4.2f, 0.1f, 0.12f), mAccent);
            for (int s = -1; s <= 1; s += 2)
            {
                P(hull, Shape.Box, new Vector3(s * 1.4f, 1.8f, -3.4f), Vector3.zero, new Vector3(0.12f, 1.1f, 0.6f), mDark);
                P(hull, Shape.Box, new Vector3(s * 2.1f, 2.4f, -3.4f), Vector3.zero, new Vector3(0.08f, 0.6f, 1.0f), mPrimary);
            }
            // Boosters
            var nozzles = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                P(hull, Shape.Cylinder, new Vector3(s * 0.9f, 0.9f, -3.9f), new Vector3(90f, 0f, 0f), new Vector3(1.0f, 0.8f, 1.0f), mMetal);
                P(hull, Shape.Cylinder, new Vector3(s * 0.9f, 0.9f, -4.33f), new Vector3(90f, 0f, 0f), new Vector3(0.75f, 0.05f, 0.75f), A.Glow(new Color(0.3f, 1f, 0.6f), 3f));
                var noz = Group(t, "Booster", new Vector3(s * 0.9f, 0.9f, -4.4f));
                noz.localRotation = Quaternion.LookRotation(Vector3.back);
                nozzles.Add(noz);
            }
            Rivets(hull, new Vector3(-1.5f, 1.42f, -3.2f), new Vector3(0.75f, 0f, 0f), 5, 0.12f);

            // Fenders (detachable)
            var fenders = new List<Transform>();
            float[] wz = { 2.6f, -2.4f };
            foreach (float z in wz)
            for (int s = -1; s <= 1; s += 2)
            {
                var f = Group(t, "Fender", new Vector3(s * 2.55f, 1.55f, z));
                P(f, Shape.Box, Vector3.zero, new Vector3(0f, 0f, -s * 10f), new Vector3(1.3f, 0.2f, 2.6f), mPrimary);
                P(f, Shape.Box, new Vector3(0f, 0.12f, 1.0f), Vector3.zero, new Vector3(1.1f, 0.05f, 0.2f), mAccent);
                MeshMerge.Merge(f);
                fenders.Add(f);
            }

            var wheels = new List<WheeledLocomotion.Wheel>();
            foreach (float z in wz)
                for (int s = -1; s <= 1; s += 2)
                    wheels.Add(BuildWheel(t, s, new Vector3(s * 2.55f, 0.2f, z), 1.2f, 1.0f));

            // Turret with rocket pods and a tesla coil
            var turretT = Group(t, "Turret", new Vector3(0f, 1.5f, -1.2f));
            var ts = Group(turretT, "TurretStatic", Vector3.zero);
            P(ts, Shape.Cylinder, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(2.4f, 0.3f, 2.4f), mDark);
            P(ts, Shape.Hex, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(2.0f, 0.7f, 2.2f), mSecondary);
            // Tesla coil: stacked rings with a glowing ball
            for (int k = 0; k < 4; k++)
                P(ts, Shape.Cylinder, new Vector3(0f, 1.1f + k * 0.35f, -0.3f), Vector3.zero, new Vector3(1.1f - k * 0.15f, 0.12f, 1.1f - k * 0.15f), k % 2 == 0 ? mMetal : A.Glow(new Color(0.5f, 0.7f, 1f), 2.5f));
            P(ts, Shape.Cylinder, new Vector3(0f, 1.6f, -0.3f), Vector3.zero, new Vector3(0.3f, 1.2f, 0.3f), mDark);
            P(ts, Shape.Sphere, new Vector3(0f, 2.65f, -0.3f), Vector3.zero, Vector3.one * 0.8f, A.Glow(new Color(0.55f, 0.75f, 1f), 4f));
            var coilTop = Group(turretT, "CoilTop", new Vector3(0f, 2.65f, -0.3f));

            var guns = Group(turretT, "Guns", new Vector3(0f, 0.7f, 0.4f));
            var gs = Group(guns, "GunStatic", Vector3.zero);
            var rocketMuzzles = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                P(gs, Shape.Box, new Vector3(s * 1.5f, 0f, 0.4f), Vector3.zero, new Vector3(0.9f, 0.9f, 1.8f), mPrimary);
                P(gs, Shape.Box, new Vector3(s * 1.5f, 0.5f, 0.4f), Vector3.zero, new Vector3(0.7f, 0.1f, 1.6f), mAccent);
                for (int r = 0; r < 2; r++)
                for (int c = 0; c < 2; c++)
                {
                    Vector3 tp = new Vector3(s * 1.5f + (c - 0.5f) * 0.4f, (r - 0.5f) * 0.4f, 1.32f);
                    P(gs, Shape.Cylinder, tp, new Vector3(90f, 0f, 0f), new Vector3(0.3f, 0.05f, 0.3f), mBlack);
                    rocketMuzzles.Add(Group(guns, "RocketMuzzle", tp + Vector3.forward * 0.2f));
                }
            }

            var smoke = Group(t, "SmokePoint", new Vector3(0f, 1.6f, -2.6f));
            MeshMerge.Merge(hull);
            MeshMerge.Merge(ts);
            MeshMerge.Merge(gs);

            root.AddComponent<RobotCommands>();
            var dmg = root.AddComponent<Damageable>();
            dmg.displayName = name;
            dmg.team = team;
            dmg.maxHealth = 1500f;
            dmg.size = 1.6f;
            dmg.smokePoint = smoke;
            dmg.detachableParts = new List<Transform> { fenders[0], fenders[3], fenders[1], fenders[2] };
            dmg.frontArmor = 0.5f;
            ResetHealth(dmg);

            var drive = root.AddComponent<WheeledLocomotion>();
            drive.wheels = wheels.ToArray();
            rb.centerOfMass = new Vector3(0f, 0.2f, 0f);
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

            var turret = root.AddComponent<Turret>();
            turret.yawPivot = turretT;
            turret.pitchPivot = guns;
            turret.yawSpeed = 120f;
            turret.pitchSpeed = 60f;
            turret.maxYaw = 170f;
            turret.acquireCone = player ? 80f : 180f;

            var pods = root.AddComponent<RocketPods>();
            pods.displayName = "Rocket Pods";
            pods.slot = Weapon.Slot.Primary;
            pods.muzzles = rocketMuzzles.ToArray();
            pods.cooldown = 0.26f;
            pods.color = new Color(1f, 0.6f, 0.2f);

            var tesla = root.AddComponent<TeslaStorm>();
            tesla.displayName = "Tesla Storm";
            tesla.slot = Weapon.Slot.Special;
            tesla.muzzles = new[] { coilTop };
            tesla.cooldown = 8f;
            tesla.color = new Color(0.55f, 0.75f, 1f);

            return root;
        }

        // =====================================================================
        // JUGGERNAUT: eight-wheeled heavy tank with flamethrowers and missiles
        // =====================================================================

        static GameObject BuildJuggernaut(Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(groundPos + Vector3.up * 3.0f, rotation);
            var t = root.transform;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 45000f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            AddBox(t, new Vector3(0f, 1.2f, 0.4f), new Vector3(6.4f, 2.8f, 13.4f));
            AddBox(t, new Vector3(0f, 3.3f, -0.8f), new Vector3(4.4f, 1.8f, 5f));

            var hull = Group(t, "HullStatic", Vector3.zero);
            P(hull, Shape.Box, new Vector3(0f, 1.2f, 0f), Vector3.zero, new Vector3(5.6f, 2.0f, 11.6f), mPrimary);
            P(hull, Shape.Box, new Vector3(0f, 0.3f, 0f), Vector3.zero, new Vector3(4.6f, 1.0f, 11.0f), mDark);
            P(hull, Shape.Box, new Vector3(0f, 2.3f, -0.5f), Vector3.zero, new Vector3(4.8f, 0.3f, 9.0f), mSecondary);
            // Dozer plow on the front
            P(hull, Shape.Wedge, new Vector3(0f, 1.0f, 6.5f), Vector3.zero, new Vector3(6.6f, 2.2f, 1.6f), mSecondary);
            P(hull, Shape.Box, new Vector3(0f, 0.2f, 7.2f), new Vector3(-30f, 0f, 0f), new Vector3(6.8f, 1.2f, 0.3f), mMetal);
            for (int i = -2; i <= 2; i++)
            {
                P(hull, Shape.Box, new Vector3(i * 1.3f, 1.0f, 7.3f), new Vector3(-25f, 0f, 0f), new Vector3(0.2f, 2.0f, 0.3f), A.Paint(new Color(0.95f, 0.72f, 0.08f), 0.1f, 0.4f, false));
                P(hull, Shape.Wedge, new Vector3(i * 1.3f, 0.1f, 7.6f), Vector3.zero, new Vector3(0.35f, 0.4f, 0.8f), mMetal);
            }
            // Smoke stacks and engine grille
            for (int s = -1; s <= 1; s += 2)
            {
                P(hull, Shape.Cylinder, new Vector3(s * 2.0f, 3.6f, -4.6f), Vector3.zero, new Vector3(0.6f, 2.6f, 0.6f), mDark);
                P(hull, Shape.Cylinder, new Vector3(s * 2.0f, 4.95f, -4.6f), Vector3.zero, new Vector3(0.5f, 0.1f, 0.5f), mHot);
                P(hull, Shape.Box, new Vector3(s * 2.9f, 1.4f, 0f), Vector3.zero, new Vector3(0.2f, 0.4f, 10.5f), mAccent);
                for (int k = 0; k < 6; k++)
                    P(hull, Shape.Box, new Vector3(s * 2.86f, 0.75f, -4f + k * 1.6f), Vector3.zero, new Vector3(0.15f, 0.7f, 0.25f), mBlack);
                P(hull, Shape.Box, new Vector3(s * 2.0f, 1.7f, 5.85f), Vector3.zero, new Vector3(0.7f, 0.35f, 0.12f), A.Glow(Color.white, 3f));
            }
            for (int k = 0; k < 7; k++)
                P(hull, Shape.Box, new Vector3(0f, 2.5f, -5.3f + k * 0.35f), Vector3.zero, new Vector3(3.6f, 0.12f, 0.15f), mBlack);
            Rivets(hull, new Vector3(-2.2f, 2.47f, 3.8f), new Vector3(1.1f, 0f, 0f), 5, 0.16f);
            Rivets(hull, new Vector3(-2.2f, 2.47f, -4.6f), new Vector3(1.1f, 0f, 0f), 5, 0.16f);
            var nozzles = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                P(hull, Shape.Cylinder, new Vector3(s * 1.4f, 1.3f, -6.0f), new Vector3(90f, 0f, 0f), new Vector3(1.6f, 0.8f, 1.6f), mMetal);
                P(hull, Shape.Cylinder, new Vector3(s * 1.4f, 1.3f, -6.42f), new Vector3(90f, 0f, 0f), new Vector3(1.2f, 0.05f, 1.2f), A.Glow(new Color(1f, 0.5f, 0.2f), 3f));
                var noz = Group(t, "Booster", new Vector3(s * 1.4f, 1.3f, -6.5f));
                noz.localRotation = Quaternion.LookRotation(Vector3.back);
                nozzles.Add(noz);
            }

            // Armoured skirts (detachable)
            var skirts = new List<Transform>();
            float[] sz = { 3.2f, -2.6f };
            foreach (float z in sz)
            for (int s = -1; s <= 1; s += 2)
            {
                var sk = Group(t, "Skirt", new Vector3(s * 3.55f, 1.6f, z));
                P(sk, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(0.4f, 1.4f, 5.2f), mPrimary);
                P(sk, Shape.Box, new Vector3(s * 0.22f, 0.4f, 0f), Vector3.zero, new Vector3(0.05f, 0.18f, 4.8f), mAccent);
                Rivets(sk, new Vector3(s * 0.22f, -0.35f, -2.2f), new Vector3(0f, 0f, 1.1f), 5, 0.12f);
                MeshMerge.Merge(sk);
                skirts.Add(sk);
            }

            var wheels = new List<WheeledLocomotion.Wheel>();
            float[] wz = { 4.6f, 1.6f, -1.4f, -4.4f };
            foreach (float z in wz)
                for (int s = -1; s <= 1; s += 2)
                    wheels.Add(BuildWheel(t, s, new Vector3(s * 3.4f, 0.4f, z), 1.5f, 1.3f));

            // Turret: flamethrowers + missile box
            var turretT = Group(t, "Turret", new Vector3(0f, 2.5f, -0.5f));
            var ts = Group(turretT, "TurretStatic", Vector3.zero);
            P(ts, Shape.Cylinder, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(4.2f, 0.4f, 4.2f), mDark);
            P(ts, Shape.Hex, new Vector3(0f, 1.0f, 0f), Vector3.zero, new Vector3(3.8f, 1.5f, 4.4f), mPrimary);
            P(ts, Shape.Box, new Vector3(0f, 1.85f, -0.5f), Vector3.zero, new Vector3(2.2f, 0.25f, 2.4f), mSecondary);
            // Fuel tanks on the back of the turret
            for (int s = -1; s <= 1; s += 2)
            {
                P(ts, Shape.Cylinder, new Vector3(s * 0.9f, 1.0f, -2.4f), new Vector3(0f, 0f, 90f), new Vector3(1.1f, 0.9f, 1.1f), A.Paint(new Color(0.75f, 0.2f, 0.1f), 0.4f, 0.5f, false));
                P(ts, Shape.Box, new Vector3(s * 0.9f, 1.0f, -2.4f), Vector3.zero, new Vector3(0.95f, 0.12f, 1.15f), mDark);
            }
            // Missile box on the roof
            var mbox = Group(turretT, "MissileBox", new Vector3(0f, 2.4f, -0.4f));
            P(ts, Shape.Box, new Vector3(0f, 2.3f, -0.4f), Vector3.zero, new Vector3(2.4f, 0.8f, 2.0f), mSecondary);
            var tubes = new List<Transform>();
            for (int r = 0; r < 2; r++)
            for (int c = 0; c < 4; c++)
            {
                Vector3 tp = new Vector3((c - 1.5f) * 0.55f, 2.72f, 0.3f - r * 0.8f);
                P(ts, Shape.Cylinder, tp, Vector3.zero, new Vector3(0.38f, 0.06f, 0.38f), mBlack);
                var tube = Group(turretT, "Tube", tp + Vector3.up * 0.1f);
                tube.localRotation = Quaternion.Euler(-65f, 0f, 0f);
                tubes.Add(tube);
            }

            var guns = Group(turretT, "Guns", new Vector3(0f, 1.0f, 1.8f));
            var gs = Group(guns, "GunStatic", Vector3.zero);
            var flameMuzzles = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                P(gs, Shape.Box, new Vector3(s * 1.0f, 0f, 0.6f), Vector3.zero, new Vector3(0.9f, 0.9f, 1.8f), mSecondary);
                P(gs, Shape.Cylinder, new Vector3(s * 1.0f, 0f, 2.0f), new Vector3(90f, 0f, 0f), new Vector3(0.55f, 1.6f, 0.55f), mDark);
                P(gs, Shape.Cylinder, new Vector3(s * 1.0f, 0f, 3.5f), new Vector3(90f, 0f, 0f), new Vector3(0.85f, 0.3f, 0.85f), mMetal);
                P(gs, Shape.Cylinder, new Vector3(s * 1.0f, 0f, 3.68f), new Vector3(90f, 0f, 0f), new Vector3(0.55f, 0.05f, 0.55f), mHot);
                P(gs, Shape.Cylinder, new Vector3(s * 1.0f, -0.55f, 1.6f), new Vector3(90f, 0f, 0f), new Vector3(0.2f, 1.4f, 0.2f), mBlack);
                flameMuzzles.Add(Group(guns, "FlameMuzzle", new Vector3(s * 1.0f, 0f, 3.8f)));
            }

            var smoke = Group(t, "SmokePoint", new Vector3(0f, 2.8f, -3.6f));
            MeshMerge.Merge(hull);
            MeshMerge.Merge(ts);
            MeshMerge.Merge(gs);

            root.AddComponent<RobotCommands>();
            var dmg = root.AddComponent<Damageable>();
            dmg.displayName = name;
            dmg.team = team;
            dmg.maxHealth = 3100f;
            dmg.size = 2.6f;
            dmg.smokePoint = smoke;
            dmg.detachableParts = new List<Transform> { skirts[0], skirts[3], skirts[1], skirts[2] };
            dmg.frontArmor = 0.35f;
            ResetHealth(dmg);

            var drive = root.AddComponent<WheeledLocomotion>();
            drive.wheels = wheels.ToArray();
            rb.centerOfMass = new Vector3(0f, 0.3f, 0.3f);
            drive.extraAirGravity = 0.7f;
            drive.thrusterNozzles = nozzles.ToArray();
            drive.wheelRadius = 1.5f;
            drive.suspensionTravel = 1.2f;
            drive.driveAccel = 7f;
            drive.maxSpeed = 19f;
            drive.turnRate = 0.95f;
            drive.boostAccel = 20f;
            drive.boostMaxSpeed = 46f;

            var turret = root.AddComponent<Turret>();
            turret.yawPivot = turretT;
            turret.pitchPivot = guns;
            turret.yawSpeed = 60f;
            turret.pitchSpeed = 35f;
            turret.maxYaw = 140f;
            turret.acquireCone = player ? 80f : 180f;

            var flame = root.AddComponent<Flamethrower>();
            flame.displayName = "Twin Flamethrowers";
            flame.slot = Weapon.Slot.Primary;
            flame.muzzles = flameMuzzles.ToArray();
            flame.cooldown = 0.1f;
            flame.color = new Color(1f, 0.5f, 0.1f);

            var missiles = root.AddComponent<MissileSwarm>();
            missiles.displayName = "Missile Box";
            missiles.slot = Weapon.Slot.Special;
            missiles.muzzles = tubes.ToArray();
            missiles.cooldown = 8f;
            missiles.missilesPerSalvo = 8;

            return root;
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
            P(ws, Shape.Cylinder, new Vector3(side * width * 0.52f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(d * 0.6f, 0.1f, d * 0.6f), mMetal);
            P(ws, Shape.Cylinder, new Vector3(side * width * 0.58f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(d * 0.25f, 0.12f, d * 0.25f), mAccent);
            for (int k = 0; k < 6; k++)
                P(ws, Shape.Box, new Vector3(side * width * 0.55f, 0f, 0f), new Vector3(k * 60f, 0f, 0f), new Vector3(0.08f, d * 0.55f, 0.18f), mDark);
            for (int k = 0; k < 12; k++)
            {
                float a = k * 30f * Mathf.Deg2Rad;
                P(ws, Shape.Box, new Vector3(0f, Mathf.Cos(a) * radius * 0.98f, Mathf.Sin(a) * radius * 0.98f), new Vector3(k * 30f, 0f, 0f), new Vector3(width * 1.02f, 0.12f, radius * 0.25f), mRubber);
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
