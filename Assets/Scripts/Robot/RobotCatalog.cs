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
                previewDistance = 42f, previewHeight = 9f
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
                previewDistance = 30f, previewHeight = 3f
            },
        };

        public static RobotInfo Get(RobotType type)
        {
            foreach (var r in All) if (r.type == type) return r;
            return All[0];
        }
    }
}
