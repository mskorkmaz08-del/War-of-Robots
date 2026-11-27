using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public static partial class RobotFactory
    {
        // =====================================================================
        // Robots built from sculpted models (Tools/mechs). Gameplay values
        // (mass, health, speeds, turret limits) match the procedural builders
        // they replace, which stay as the fallback if a model is missing.
        // =====================================================================

        class ModelSpec
        {
            public float mass, health, size, frontArmor;
            public bool fast;
            public Vector3[] boxes;              // centre, size pairs (root space)
            public System.Action<WalkerLocomotion> walker;
            public System.Action<WheeledLocomotion> wheeled;
            public Vector3 centerOfMass;
            public bool setCenterOfMass;
            public float yawSpeed, pitchSpeed, maxYaw, maxPitch = -1f, cone = 75f;
            public float armScale = 1f, shoulderScale = 1f, backScale = 1f;
            public string[] detach;
        }

        static Chassis TryModel(RobotType type, Vector3 groundPos, Quaternion rotation, int team, bool player, string name, float k)
        {
            var m = RobotModel.Load(type.ToString(), k);
            if (m == null) return null;
            var spec = Spec(type);
            if (spec == null) return null;
            spec.health = Balance.Health(type);
            if (k != 1f) ScaleSpec(spec, k);
            return BuildFromModel(m, spec, groundPos, rotation, team, player, name);
        }

        /// <summary>
        /// Everything in metres grows with the robot: collision boxes, stride, step
        /// height, wheels and suspension, weapon size. Speeds grow with the stride so
        /// the gait keeps the same slow, heavy rhythm while covering more ground.
        /// </summary>
        static void ScaleSpec(ModelSpec spec, float k)
        {
            var boxes = new Vector3[spec.boxes.Length];
            for (int i = 0; i < boxes.Length; i++) boxes[i] = spec.boxes[i] * k;
            spec.boxes = boxes;
            spec.size *= k;
            spec.centerOfMass *= k;
            spec.armScale *= k;
            spec.shoulderScale *= k;
            spec.backScale *= k;
            var walker = spec.walker;
            if (walker != null)
                spec.walker = w =>
                {
                    walker(w);
                    w.strideLength *= k;
                    w.stepHeight *= k;
                    w.walkSpeed *= k;
                    w.jetAccel *= Mathf.Sqrt(k);
                };
            var wheeled = spec.wheeled;
            spec.wheeled = d =>
            {
                wheeled?.Invoke(d);
                d.wheelRadius *= k;
                d.suspensionTravel *= k;
                d.springStiffness /= k;
                d.maxSpeed *= Mathf.Sqrt(k);
                d.boostMaxSpeed *= Mathf.Sqrt(k);
            };
        }

        static ModelSpec Spec(RobotType type)
        {
            switch (type)
            {
                case RobotType.Titan:
                    return new ModelSpec
                    {
                        mass = 50000f, health = 2600f, size = 2.5f,
                        boxes = new[] { new Vector3(0f, 3.6f, 0f), new Vector3(7f, 7.6f, 5.4f), new Vector3(0f, -3.8f, 0f), new Vector3(5.2f, 5.4f, 3.6f) },
                        walker = w => { w.strideLength = 5.5f; w.fuelDrain = 20f; w.sprintBoost = true; },
                        yawSpeed = 55f, pitchSpeed = 30f, maxYaw = 110f, cone = 75f,
                        detach = new[] { "PauldronL", "ChestL", "PauldronR", "ChestR" }
                    };
                case RobotType.Lancer:
                    return new ModelSpec
                    {
                        mass = 32000f, health = 1700f, size = 1.8f,
                        boxes = new[] { new Vector3(0f, 2.6f, 0.1f), new Vector3(5.0f, 5.0f, 4.0f), new Vector3(0f, -3.4f, 0f), new Vector3(3.6f, 5f, 3f) },
                        walker = w => { w.strideLength = 5f; w.stepDuration = 0.42f; w.walkSpeed = 11f; w.turnRate = 1.0f; w.fuelDrain = 35f; },
                        yawSpeed = 70f, pitchSpeed = 40f, maxYaw = 120f, cone = 70f,
                        armScale = 0.85f, shoulderScale = 0.85f, backScale = 0.8f,
                        detach = new[] { "ChestL", "Antenna", "ChestR", "PauldronL", "PauldronR" }
                    };
                case RobotType.Vanguard:
                    return new ModelSpec
                    {
                        mass = 38000f, health = 2000f, size = 2.0f,
                        boxes = new[] { new Vector3(0f, 2.9f, 0f), new Vector3(6.0f, 5.6f, 4.4f), new Vector3(0f, -3.4f, 0f), new Vector3(4.0f, 5f, 3f) },
                        walker = w => { w.strideLength = 5f; w.stepDuration = 0.45f; w.walkSpeed = 9.5f; w.turnRate = 0.95f; w.jetAccel = 26f; w.fuelDrain = 32f; },
                        yawSpeed = 65f, pitchSpeed = 36f, maxYaw = 115f, cone = 75f,
                        armScale = 0.92f, shoulderScale = 0.9f, backScale = 0.85f,
                        detach = new[] { "ChestL", "ChestR", "PauldronL", "PauldronR" }
                    };
                case RobotType.Arachne:
                    return new ModelSpec
                    {
                        mass = 36000f, health = 2100f, size = 2.2f,
                        boxes = new[] { new Vector3(0f, 0.6f, -0.6f), new Vector3(6f, 3.2f, 8f) },
                        walker = w => { w.strideLength = 4.6f; w.stepDuration = 0.36f; w.stepHeight = 1.6f; w.walkSpeed = 10f; w.acceleration = 4.5f; w.turnRate = 1.15f; w.fuelDrain = 40f; },
                        yawSpeed = 85f, pitchSpeed = 45f, maxYaw = 160f, maxPitch = 35f, cone = 80f,
                        armScale = 0.85f, shoulderScale = 0.8f, backScale = 0.9f,
                        detach = new[] { "ChestL", "Antenna", "ChestR" }
                    };
                case RobotType.Mammoth:
                    return new ModelSpec
                    {
                        mass = 58000f, health = 3300f, size = 2.7f,
                        boxes = new[] { new Vector3(0f, 0.9f, 0f), new Vector3(5.6f, 3.4f, 10.4f), new Vector3(0f, 3.2f, 2.6f), new Vector3(4.0f, 2.0f, 3.6f) },
                        walker = w => { w.strideLength = 5.2f; w.stepDuration = 0.5f; w.stepHeight = 1.5f; w.walkSpeed = 7f; w.acceleration = 2.8f; w.turnRate = 0.65f; w.jetAccel = 18f; w.fuelDrain = 20f; w.sprintBoost = true; },
                        yawSpeed = 50f, pitchSpeed = 30f, maxYaw = 150f, cone = 75f,
                        detach = new[] { "ChestL", "Antenna", "ChestR" }
                    };
                case RobotType.Raptor:
                    return Wheeled(30000f, true, 2000f, 2f, 0.3f, new Vector3(0f, 2.35f, -0.6f), 0.85f, new Vector3(0f, 0.8f, 0.2f), new Vector3(5.4f, 2.0f, 10.4f),
                        new Vector3(0f, 0.3f, 0.2f), d => { d.extraAirGravity = 0.6f; d.wheelRadius = 1.4f; d.suspensionTravel = 1.1f; }, 90f, 45f, 150f);
                case RobotType.Hornet:
                    return Wheeled(18000f, true, 1500f, 1.6f, 0.5f, new Vector3(0f, 2.05f, -0.5f), 0.68f, new Vector3(0f, 0.7f, 0f), new Vector3(4.4f, 1.6f, 8.4f),
                        new Vector3(0f, 0.2f, 0f), d =>
                        {
                            d.extraAirGravity = 0.6f; d.wheelRadius = 1.2f; d.suspensionTravel = 0.9f; d.driveAccel = 13f; d.maxSpeed = 33f;
                            d.turnRate = 1.7f; d.lateralGrip = 8f; d.boostAccel = 30f; d.boostMaxSpeed = 74f;
                        }, 120f, 60f, 170f);
                case RobotType.Juggernaut:
                    return Wheeled(45000f, false, 3100f, 2.6f, 0.35f, new Vector3(0f, 2.85f, -0.9f), 1.05f, new Vector3(0f, 1.0f, 0.4f), new Vector3(6.4f, 2.6f, 13.4f),
                        new Vector3(0f, 0.3f, 0.3f), d =>
                        {
                            d.extraAirGravity = 0.7f; d.wheelRadius = 1.5f; d.suspensionTravel = 1.2f; d.driveAccel = 7f; d.maxSpeed = 19f;
                            d.turnRate = 0.95f; d.boostAccel = 20f; d.boostMaxSpeed = 46f;
                        }, 60f, 35f, 140f);
                case RobotType.Bastion:
                    return Wheeled(38000f, false, 2800f, 2.1f, 0.5f, new Vector3(0f, 2.7f, -1.0f), 0.95f, new Vector3(0f, 1.0f, 0f), new Vector3(6.0f, 2.4f, 11.6f),
                        new Vector3(0f, 0.3f, 0f), d =>
                        {
                            d.extraAirGravity = 0.6f; d.wheelRadius = 1.35f; d.suspensionTravel = 0.9f; d.driveAccel = 8f; d.maxSpeed = 22f;
                            d.turnRate = 1.0f; d.lateralGrip = 9f; d.boostAccel = 18f; d.boostMaxSpeed = 42f;
                        }, 55f, 30f, 180f);
            }
            return null;
        }

        static ModelSpec Wheeled(float mass, bool fast, float health, float size, float front, Vector3 waist, float k, Vector3 hullC, Vector3 hullS,
            Vector3 com, System.Action<WheeledLocomotion> drive, float yaw, float pitch, float maxYaw)
        {
            return new ModelSpec
            {
                mass = mass, fast = fast, health = health, size = size, frontArmor = front,
                boxes = new[] { hullC, hullS, waist + new Vector3(0f, 2.3f, -0.3f) * k, new Vector3(4.4f, 4.4f, 3.6f) * k },
                wheeled = drive, centerOfMass = com, setCenterOfMass = true,
                yawSpeed = yaw, pitchSpeed = pitch, maxYaw = maxYaw, cone = 80f,
                armScale = 0.82f * k, shoulderScale = 0.82f * k, backScale = 0.8f * k
            };
        }

        static Chassis BuildFromModel(RobotModel m, ModelSpec spec, Vector3 groundPos, Quaternion rotation, int team, bool player, string name)
        {
            float stand = m.info.stand;
            var root = NewRoot(name, groundPos + Vector3.up * stand, rotation, spec.mass, spec.fast);
            var t = root.transform;
            for (int i = 0; i + 1 < spec.boxes.Length; i += 2) AddBox(t, spec.boxes[i], spec.boxes[i + 1]);

            var rootOrigin = new Vector3(0f, stand, 0f);
            var upper = Group(t, "Upper", m.Upper - rootOrigin);
            var arms = Group(upper, "Arms", m.Arms - m.Upper);

            var anchors = new Dictionary<string, Transform>();
            var made = PlaceModel(m, t, upper, arms, anchors);

            var mounts = new[]
            {
                AnchorOr(anchors, "MountL", arms, new Vector3(-2f, -2f, 1.5f)),
                AnchorOr(anchors, "MountR", arms, new Vector3(2f, -2f, 1.5f))
            };
            var shoulder = AnchorOr(anchors, "Shoulder", arms, new Vector3(-2f, 1.5f, -0.2f));
            Transform[] backs;
            if (anchors.TryGetValue("Back", out var back)) backs = new[] { back };
            else backs = new[] { AnchorOr(anchors, "BackL", upper, new Vector3(-1.5f, 4f, -1.5f)), AnchorOr(anchors, "BackR", upper, new Vector3(1.5f, 4f, -1.5f)) };
            var smoke = AnchorOr(anchors, "Smoke", upper, new Vector3(0f, 2.5f, -1f));

            var detach = new List<Transform>();
            var names = m.info.detach != null && m.info.detach.Length > 0 ? m.info.detach : spec.detach;
            if (names != null)
                foreach (var n in names)
                    if (made.TryGetValue(n, out var d)) detach.Add(d);
            AddDamageable(root, name, team, spec.health, spec.size, smoke, detach, spec.frontArmor);

            if (spec.walker != null)
            {
                var legs = ModelLegs(m, made, anchors);
                var jets = new List<Transform>();
                foreach (var j in new[] { "JetL", "JetR" })
                    if (anchors.TryGetValue(j, out var jt)) jets.Add(jt);
                var walker = root.AddComponent<WalkerLocomotion>();
                walker.legs = legs.ToArray();
                walker.jetNozzles = jets.ToArray();
                walker.standHeight = stand;
                walker.thighLength = m.info.thigh;
                walker.shinLength = m.info.shin;
                walker.ankleHeight = m.info.ankle;
                spec.walker(walker);
            }
            else
            {
                var wheels = new List<WheeledLocomotion.Wheel>();
                for (int i = 0; i < m.info.wheels; i++)
                {
                    made.TryGetValue("Wheel" + i, out var vis);
                    anchors.TryGetValue("WheelAnchor" + i, out var anc);
                    if (vis != null && anc != null) wheels.Add(new WheeledLocomotion.Wheel { anchor = anc, visual = vis });
                }
                var nozzles = new List<Transform>();
                for (int i = 0; anchors.TryGetValue("Booster" + i, out var b); i++) nozzles.Add(b);
                var drive = root.AddComponent<WheeledLocomotion>();
                drive.wheels = wheels.ToArray();
                drive.thrusterNozzles = nozzles.ToArray();
                if (spec.setCenterOfMass) root.GetComponent<Rigidbody>().centerOfMass = spec.centerOfMass;
                spec.wheeled?.Invoke(drive);
            }

            var turret = root.AddComponent<Turret>();
            turret.yawPivot = upper;
            turret.pitchPivot = arms;
            turret.yawSpeed = spec.yawSpeed;
            turret.pitchSpeed = spec.pitchSpeed;
            turret.maxYaw = spec.maxYaw;
            if (spec.maxPitch > 0f) turret.maxPitch = spec.maxPitch;
            turret.acquireCone = player ? spec.cone : 180f;

            return new Chassis
            {
                root = root, turret = turret, armMounts = mounts, shoulderMount = shoulder, backMounts = backs,
                armScale = spec.armScale, shoulderScale = spec.shoulderScale, backScale = spec.backScale
            };
        }
    }
}
