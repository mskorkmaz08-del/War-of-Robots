using System.Collections.Generic;

namespace RobotArena
{
    /// <summary>Everything the hangar screen shows about each robot.</summary>
    public class RobotInfo
    {
        public RobotType type;
        public string name;
        public string role;
        public string description;
        public string primary;
        public string special;
        public string mobility;
        // 0..1 for the stat bars.
        public float armour;
        public float speed;
        public float firepower;
        public float range;
        public float previewDistance = 40f;
        public float previewHeight = 9f;
        // Follow camera framing in a fight.
        public float camDistance = 24f;
        public float camHeight = 0f;
        public float camPivot = 2.5f;
        // How the AI drives this robot when it is an enemy.
        public float aiPreferredRange = 50f;
        public float aiFireRange = 150f;
        public float aiSpecialRange = 160f;
        public float aiAggression = 0.6f;
        public bool aiBoostCharge;
    }

    public static class RobotCatalog
    {
        public static readonly List<RobotInfo> All = new List<RobotInfo>
        {
            new RobotInfo
            {
                type = RobotType.Titan,
                name = "TITAN",
                role = "HEAVY WALKER",
                description = "A 15-metre walking fortress. Slow, huge and very hard to stop.",
                primary = "Twin heavy cannons",
                special = "12-missile homing swarm",
                mobility = "Jump jets + shockwave landing",
                armour = 0.95f, speed = 0.35f, firepower = 0.85f, range = 0.7f,
                previewDistance = 42f, previewHeight = 9f,
                camDistance = 34f, camHeight = -2f, camPivot = 3f,
                aiPreferredRange = 70f, aiFireRange = 190f, aiSpecialRange = 160f, aiAggression = 0.5f
            },
            new RobotInfo
            {
                type = RobotType.Raptor,
                name = "RAPTOR",
                role = "ASSAULT TANK",
                description = "An armoured six-wheeler built to ram. Fast, brutal up close.",
                primary = "Rapid pulse lasers",
                special = "Plasma railgun",
                mobility = "ULTRA THRUST rockets",
                armour = 0.7f, speed = 0.9f, firepower = 0.7f, range = 0.85f,
                previewDistance = 30f, previewHeight = 3f,
                aiPreferredRange = 45f, aiFireRange = 150f, aiSpecialRange = 180f, aiAggression = 0.7f, aiBoostCharge = true
            },
            new RobotInfo
            {
                type = RobotType.Arachne,
                name = "ARACHNE",
                role = "SPIDER WALKER",
                description = "Four long legs and a spinning gun nest. Steady aim, rains shells from above.",
                primary = "Twin gatling guns",
                special = "6-shell mortar barrage",
                mobility = "Jump jets",
                armour = 0.75f, speed = 0.5f, firepower = 0.8f, range = 0.8f,
                previewDistance = 40f, previewHeight = 7f,
                camDistance = 30f, camHeight = -1f, camPivot = 3f,
                aiPreferredRange = 60f, aiFireRange = 170f, aiSpecialRange = 150f, aiAggression = 0.55f
            },
            new RobotInfo
            {
                type = RobotType.Hornet,
                name = "HORNET",
                role = "LIGHT RAIDER",
                description = "A light four-wheeler. The fastest thing in the arena, with a lightning coil on its back.",
                primary = "Rapid rocket pods",
                special = "Tesla storm (chains 3 targets)",
                mobility = "Afterburner boost",
                armour = 0.45f, speed = 1f, firepower = 0.65f, range = 0.6f,
                previewDistance = 26f, previewHeight = 3f,
                camDistance = 22f, camHeight = 0f, camPivot = 2.2f,
                aiPreferredRange = 40f, aiFireRange = 130f, aiSpecialRange = 120f, aiAggression = 0.8f, aiBoostCharge = true
            },
            new RobotInfo
            {
                type = RobotType.Juggernaut,
                name = "JUGGERNAUT",
                role = "SIEGE CRAWLER",
                description = "Eight wheels, a dozer blade and flamethrowers. Gets close and burns everything.",
                primary = "Twin flamethrowers",
                special = "8-missile box launcher",
                mobility = "Ram boost",
                armour = 1f, speed = 0.4f, firepower = 0.95f, range = 0.35f,
                previewDistance = 36f, previewHeight = 4f,
                camDistance = 30f, camHeight = -1f, camPivot = 3f,
                aiPreferredRange = 22f, aiFireRange = 45f, aiSpecialRange = 170f, aiAggression = 0.9f, aiBoostCharge = true
            },
        };

        public static RobotInfo Get(RobotType type)
        {
            foreach (var r in All) if (r.type == type) return r;
            return All[0];
        }
    }
}
