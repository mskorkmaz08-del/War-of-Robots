using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public enum GameMode { TeamBattle, Domination, Survival, Mission }
    public enum MapId { Foundry, Dockyard, Canyon }

    public class ModeInfo
    {
        public GameMode mode;
        public string name;
        public string tagline;
        public string description;
    }

    public class MapInfo
    {
        public MapId map;
        public string name;
        public string description;
    }

    /// <summary>What kind of objective a mission uses.</summary>
    public enum MissionGoal { EliminateAll, DestroyDepots, DefendReactor, HoldUplink, KillCommander, SurviveWaves }

    public class MissionInfo
    {
        public string name;
        public string briefing;
        public MapId map;
        public MissionGoal goal;
        public int allies;
        public RobotType[] enemies = new RobotType[0];
        /// <summary>Seconds to hold or defend, or the number of waves.</summary>
        public float amount;
    }

    /// <summary>
    /// The chosen mode, map and mission. Static so it survives the scene reload used
    /// for "fight again". Also the catalogue of modes, maps and missions shown in the menus.
    /// </summary>
    public static class MatchSetup
    {
        public static GameMode Mode = GameMode.TeamBattle;
        public static MapId Map = MapId.Foundry;
        public static int MissionIndex;

        public static MissionInfo Mission => Missions[Mathf.Clamp(MissionIndex, 0, Missions.Count - 1)];

        public static readonly List<ModeInfo> Modes = new List<ModeInfo>
        {
            new ModeInfo { mode = GameMode.TeamBattle, name = "TEAM BATTLE", tagline = "3 VS 3",
                description = "You and two allied robots against three enemies. Destroy the whole enemy team." },
            new ModeInfo { mode = GameMode.Domination, name = "DOMINATION", tagline = "CAPTURE POINTS",
                description = "Capture and hold points A, B and C. Robots respawn. First team to 100 points wins." },
            new ModeInfo { mode = GameMode.Survival, name = "SURVIVAL", tagline = "5 WAVES",
                description = "Stand with two allies against five growing waves of enemy robots." },
            new ModeInfo { mode = GameMode.Mission, name = "MISSIONS", tagline = "CAMPAIGN",
                description = "Six missions with objectives: raids, defence, capture and a commander hunt." },
        };

        public static readonly List<MapInfo> Maps = new List<MapInfo>
        {
            new MapInfo { map = MapId.Foundry, name = "FOUNDRY", description = "Walled steel arena with a central tower, ramps and pillars." },
            new MapInfo { map = MapId.Dockyard, name = "DOCKYARD", description = "Container stacks, cranes and warehouses. Tight lanes, lots of cover." },
            new MapInfo { map = MapId.Canyon, name = "CANYON OUTPOST", description = "Desert outpost between rock mesas. Open ground and long sightlines." },
        };

        public static readonly List<MissionInfo> Missions = new List<MissionInfo>
        {
            new MissionInfo { name = "FIRST CONTACT", map = MapId.Foundry, goal = MissionGoal.EliminateAll, allies = 1,
                enemies = new[] { RobotType.Hornet, RobotType.Hornet },
                briefing = "Two enemy scouts are probing the Foundry. Hunt them down." },
            new MissionInfo { name = "SUPPLY RAID", map = MapId.Dockyard, goal = MissionGoal.DestroyDepots, allies = 1,
                enemies = new[] { RobotType.Raptor, RobotType.Arachne },
                briefing = "Destroy the three enemy fuel tanks in the dockyard. Expect guards." },
            new MissionInfo { name = "HOLD THE LINE", map = MapId.Canyon, goal = MissionGoal.DefendReactor, allies = 2, amount = 150f,
                enemies = new[] { RobotType.Hornet, RobotType.Raptor, RobotType.Titan, RobotType.Bastion },
                briefing = "Keep the outpost reactor alive until reinforcements arrive." },
            new MissionInfo { name = "UPLINK", map = MapId.Foundry, goal = MissionGoal.HoldUplink, allies = 2, amount = 60f,
                enemies = new[] { RobotType.Arachne, RobotType.Lancer, RobotType.Vanguard },
                briefing = "Capture the uplink at the central tower and hold it for 60 seconds." },
            new MissionInfo { name = "HEADHUNTER", map = MapId.Dockyard, goal = MissionGoal.KillCommander, allies = 2,
                enemies = new[] { RobotType.Bastion, RobotType.Lancer, RobotType.Mammoth },
                briefing = "The enemy commander WARLORD is in the dockyard. Destroy him." },
            new MissionInfo { name = "LAST STAND", map = MapId.Canyon, goal = MissionGoal.SurviveWaves, allies = 1, amount = 6f,
                enemies = new RobotType[0],
                briefing = "Cut off in the canyon. Survive six waves." },
        };

        // ---- Saved progress ----

        const string ProgressKey = "wor_missions_done";

        /// <summary>Number of missions completed (mission N is unlocked when N-1 is done).</summary>
        public static int MissionsCompleted
        {
            get => PlayerPrefs.GetInt(ProgressKey, 0);
            private set { PlayerPrefs.SetInt(ProgressKey, value); PlayerPrefs.Save(); }
        }

        public static bool IsUnlocked(int index) => index <= MissionsCompleted;
        public static bool IsDone(int index) => index < MissionsCompleted;

        public static void CompleteMission(int index)
        {
            if (index + 1 > MissionsCompleted) MissionsCompleted = index + 1;
        }
    }
}
