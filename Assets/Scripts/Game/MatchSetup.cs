using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public enum GameMode { TeamBattle, Domination, Survival, Mission, KingOfTheHill, Siege, Escort, FreeForAll }
    public enum MapId { Foundry, Dockyard, Canyon, FrozenPass, RuinedCity, Refinery, Highlands, LaunchSite, Airbase }

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
    public enum MissionGoal { EliminateAll, DestroyDepots, DefendReactor, HoldUplink, KillCommander, SurviveWaves, EscortVip, Sabotage, Extraction, Duel }

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
        /// <summary>The enemy commander WARLORD takes the field too.</summary>
        public bool boss;
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
                description = "You and two allies against three enemies. Destroy the whole enemy team." },
            new ModeInfo { mode = GameMode.Domination, name = "DOMINATION", tagline = "CAPTURE POINTS",
                description = "Capture and hold points A, B and C. Robots respawn. First to 100 points wins." },
            new ModeInfo { mode = GameMode.KingOfTheHill, name = "KING OF THE HILL", tagline = "ONE MOVING ZONE",
                description = "Hold the hill to score. It moves every 80 seconds. Robots respawn. First to 100." },
            new ModeInfo { mode = GameMode.Siege, name = "SIEGE", tagline = "BASE ASSAULT",
                description = "Destroy both enemy power cores while you defend your own. Robots respawn." },
            new ModeInfo { mode = GameMode.Escort, name = "ESCORT", tagline = "PROTECT THE WALKER",
                description = "Walk the damaged command walker ATLAS PRIME to extraction. It moves only with you near." },
            new ModeInfo { mode = GameMode.FreeForAll, name = "FREE FOR ALL", tagline = "6 MACHINES",
                description = "Every robot for itself. First to 8 kills, or the most kills when time runs out." },
            new ModeInfo { mode = GameMode.Survival, name = "SURVIVAL", tagline = "5 WAVES",
                description = "Stand with two allies against five growing waves of enemy robots." },
            new ModeInfo { mode = GameMode.Mission, name = "MISSIONS", tagline = "CAMPAIGN",
                description = "Fourteen missions: raids, sabotage, escort, defence, duels and a final assault." },
        };

        public static readonly List<MapInfo> Maps = new List<MapInfo>
        {
            new MapInfo { map = MapId.Foundry, name = "FOUNDRY", description = "Steelworks in a ruined city: a central tower, ramps and pillars." },
            new MapInfo { map = MapId.Dockyard, name = "DOCKYARD", description = "Container stacks, cranes and warehouses. Tight lanes, lots of cover." },
            new MapInfo { map = MapId.Canyon, name = "CANYON OUTPOST", description = "Desert outpost between rock mesas. Open ground and long sightlines." },
            new MapInfo { map = MapId.FrozenPass, name = "FROZEN PASS", description = "Snowbound research base between ice ridges and radar domes." },
            new MapInfo { map = MapId.RuinedCity, name = "RUINED CITY", description = "Bombed-out downtown. Street fights between broken towers." },
            new MapInfo { map = MapId.Refinery, name = "REFINERY", description = "Fuel tanks, pipe racks and flare stacks at dusk." },
            new MapInfo { map = MapId.Highlands, name = "HIGHLAND DAM", description = "A 95 m arch dam over a green valley: turbine halls, switchyards and power lines." },
            new MapInfo { map = MapId.LaunchSite, name = "LAUNCH SITE", description = "A rocket on its launch pad, fuel spheres, tracking radars and bunkers." },
            new MapInfo { map = MapId.Airbase, name = "AIRBASE", description = "A runway down the middle, arched hangars, parked jets and a control tower." },
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
            new MissionInfo { name = "IRON CONVOY", map = MapId.Highlands, goal = MissionGoal.EscortVip, allies = 2,
                briefing = "ATLAS PRIME is crippled. Walk it past the dam to extraction. It only moves with you beside it." },
            new MissionInfo { name = "BLACKOUT", map = MapId.Airbase, goal = MissionGoal.Sabotage, allies = 2,
                enemies = new[] { RobotType.Raptor, RobotType.Vanguard, RobotType.Scarab, RobotType.Strider },
                briefing = "Two power cores feed the enemy airbase. Break through and destroy both." },
            new MissionInfo { name = "SIGNAL FIRE", map = MapId.LaunchSite, goal = MissionGoal.HoldUplink, allies = 2, amount = 90f,
                enemies = new[] { RobotType.Hornet, RobotType.Strider, RobotType.Titan, RobotType.Raptor, RobotType.Vanguard },
                briefing = "Seize the launch control uplink and hold it for 90 seconds against a counter-attack." },
            new MissionInfo { name = "ACE DUEL", map = MapId.RuinedCity, goal = MissionGoal.Duel, allies = 0,
                enemies = new[] { RobotType.Lancer },
                briefing = "Their best pilot has challenged you. One machine against one, in the ruins. No help." },
            new MissionInfo { name = "EXTRACTION", map = MapId.FrozenPass, goal = MissionGoal.Extraction, allies = 1,
                enemies = new[] { RobotType.Hornet, RobotType.Raptor, RobotType.Arachne, RobotType.Hornet, RobotType.Juggernaut, RobotType.Raptor },
                briefing = "Your unit is surrounded. Fight across the pass to the extraction point before they cut you off." },
            new MissionInfo { name = "FORTRESS", map = MapId.Highlands, goal = MissionGoal.DefendReactor, allies = 2, amount = 210f,
                enemies = new[] { RobotType.Raptor, RobotType.Titan, RobotType.Strider, RobotType.Colossus, RobotType.Arachne, RobotType.Bastion },
                briefing = "Hold the dam's reactor for three and a half minutes. The enemy is coming in force." },
            new MissionInfo { name = "SCORCHED EARTH", map = MapId.Refinery, goal = MissionGoal.DestroyDepots, allies = 2,
                enemies = new[] { RobotType.Bastion, RobotType.Scarab, RobotType.Arachne, RobotType.Vanguard },
                briefing = "The refinery fuels their whole army. Burn all three tanks. Heavy guards." },
            new MissionInfo { name = "FINAL ASSAULT", map = MapId.LaunchSite, goal = MissionGoal.Sabotage, allies = 2, boss = true,
                enemies = new[] { RobotType.Colossus, RobotType.Juggernaut, RobotType.Scarab, RobotType.Strider, RobotType.Titan },
                briefing = "Storm the launch site. Their commander WARLORD guards the power cores. End this war." },
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
