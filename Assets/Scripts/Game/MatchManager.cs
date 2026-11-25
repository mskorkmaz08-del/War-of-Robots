using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotArena
{
    /// <summary>
    /// Runs a match: robot selection, building the map, handing the rules to the
    /// chosen game mode, ending the match, slow-motion finishing blows and restarting.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public enum State { Menu, Hangar, Deploying, Fighting, Won, Lost }
        public enum Screen { Menu, Hangar, Redeploy }

        public static MatchManager Instance { get; private set; }

        // Kept for the scene built by ArenaBuilder; spawn points now come from MapBuilder.Layout.
        public Transform playerSpawn;
        public Transform[] enemySpawns = new Transform[0];

        public State Current { get; private set; } = State.Menu;
        public bool Paused { get; private set; }
        public Damageable Player { get; private set; }
        public readonly List<Damageable> Enemies = new List<Damageable>();

        Hud hud;
        const float BaseFixedDelta = 0.02f;

        // Remembered across scene reloads: where to go next, and the last robot picked.
        static Screen nextScreen = Screen.Menu;
        static RobotType lastType = RobotType.Titan;
        Showroom showroom;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = BaseFixedDelta;
            Performance.Apply();
            Performance.TuneCamera(Camera.main);
            Performance.AddReflections(transform, new Vector3(0f, 20f, 0f), new Vector3(600f, 200f, 600f));
        }

        void Start()
        {
            hud = gameObject.AddComponent<Hud>();
            hud.Build(this);
            showroom = Showroom.Create();
            // The Foundry's baked blocks get windows, doors and roofs.
            BuildingKit.DressFoundry();
            // Fill the effect pools and compile effect shaders out of sight.
            FxPool.Prewarm(new Vector3(0f, -40f, 0f));

            var screen = nextScreen;
            nextScreen = Screen.Menu;
            switch (screen)
            {
                case Screen.Redeploy:
                    Current = State.Hangar;
                    StartMatch(lastType);
                    break;
                case Screen.Hangar:
                    OpenHangar();
                    GameAudio.Prewarm();
                    break;
                default:
                    hud.ShowMenu();
                    GameAudio.Prewarm();
                    break;
            }
        }

        public RobotType PlayerType { get; private set; }

        public void OpenHangar()
        {
            if (Current != State.Menu && Current != State.Hangar) return;
            Current = State.Hangar;
            showroom.Open(RobotCatalog.Get(lastType));
            hud.ShowHangar(RobotCatalog.Get(lastType));
        }

        /// <summary>Hangar back button: return to the mode / map / mission choice.</summary>
        public void BackToMenu()
        {
            if (Current != State.Hangar) return;
            Current = State.Menu;
            showroom.Close();
            hud.ShowModeSelect();
        }

        public void PreviewRobot(RobotInfo info)
        {
            lastType = info.type;
            showroom.Show(info);
        }

        public void SpinPreview(float degrees) => showroom.Drag(degrees);

        public void StartMatch(RobotType type)
        {
            if (Current != State.Hangar) return;
            Current = State.Deploying;
            PlayerType = type;
            lastType = type;
            showroom.Close();
            hud.ShowDeploying();
            StartCoroutine(Deploy(type));
        }

        public MapLayout Layout { get; private set; }
        public ModeController Rules { get; private set; }
        public readonly List<Damageable> Allies = new List<Damageable>();
        readonly Dictionary<Damageable, RobotType> types = new Dictionary<Damageable, RobotType>();
        float lastKillSlowMo = -10f;

        static ModeController CreateRules()
        {
            switch (MatchSetup.Mode)
            {
                case GameMode.Domination: return new DominationMode();
                case GameMode.Survival: return new SurvivalMode(5, 2);
                case GameMode.Mission:
                    var m = MatchSetup.Mission;
                    int idx = Mathf.Clamp(MatchSetup.MissionIndex, 0, MatchSetup.Missions.Count - 1);
                    if (m.goal == MissionGoal.SurviveWaves) return new SurvivalMode(Mathf.RoundToInt(m.amount), m.allies, idx);
                    return new ObjectiveMode(m.goal, m.allies, m.enemies, m.amount, idx);
                default: return ObjectiveMode.TeamBattle();
            }
        }

        public static MapId CurrentMap => MatchSetup.Mode == GameMode.Mission ? MatchSetup.Mission.map : MatchSetup.Map;

        /// <summary>Builds the map, then one robot per frame so the screen never freezes on a tap.</summary>
        IEnumerator Deploy(RobotType type)
        {
            // Let the "deploying" screen draw before the heavy work starts.
            yield return null;
            yield return null;

            var map = CurrentMap;
            MapBuilder.Build(map);
            Layout = MapBuilder.Layout(map);
            yield return null;

            Rules = CreateRules();
            Rules.Init(this, Layout);
            yield return Rules.Setup();

            Current = State.Fighting;
            hud.ShowFight();
            hud.Announce(Rules.ModeName, UiKit.Orange);
            GameAudio.Prewarm();
        }

        static Quaternion FacingCentre(Vector3 pos, int team)
        {
            Vector3 to = new Vector3(-pos.x * 0.3f, 0f, -pos.z);
            if (to.sqrMagnitude < 1f) to = team == 0 ? Vector3.forward : Vector3.back;
            return Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        /// <summary>Builds the player's robot (also used to respawn it).</summary>
        public Damageable SpawnPlayer(Vector3 pos)
        {
            var type = PlayerType;
            var playerGo = RobotFactory.Build(type, pos, FacingCentre(pos, 0), 0, Livery.Player, true, RobotCatalog.Get(type).name);
            var input = playerGo.AddComponent<RobotInput>();
            hud.ConnectControls(input);
            var playerTurret = playerGo.GetComponent<Turret>();
            if (playerTurret != null) playerTurret.aimCamera = FollowCamera.Main;
            Player = playerGo.GetComponent<Damageable>();
            Player.Died += OnRobotDied;
            types[Player] = type;

            var cam = FollowCamera.Main;
            if (cam != null)
            {
                var pi = RobotCatalog.Get(type);
                cam.SetTarget(playerGo.transform, pi.camDistance, pi.camHeight, pi.camPivot);
            }
            // Teammates that follow the old wreck follow the new robot.
            foreach (var a in Allies)
            {
                var b = a != null ? a.GetComponent<AIBrain>() : null;
                if (b != null && b.follow != null && b.follow.GetComponent<RobotInput>() != null) b.follow = playerGo.transform;
            }
            if (Current == State.Fighting) hud.BindPlayer();
            return Player;
        }

        /// <summary>Builds an AI robot for either team.</summary>
        public Damageable SpawnRobot(RobotType type, int team, Vector3 pos, string name, bool altLivery = false)
        {
            var info = RobotCatalog.Get(type);
            Livery livery = team == 0 ? Livery.Ally : (altLivery || Enemies.Count % 2 == 1 ? Livery.Enemy2 : Livery.Enemy);
            var go = RobotFactory.Build(type, pos, FacingCentre(pos, team), team, livery, false, name);
            var brain = go.AddComponent<AIBrain>();
            brain.preferredRange = info.aiPreferredRange;
            brain.fireRange = info.aiFireRange;
            brain.specialRange = info.aiSpecialRange;
            brain.aggression = info.aiAggression;
            brain.usesBoostToCharge = info.aiBoostCharge;
            var d = go.GetComponent<Damageable>();
            d.Died += OnRobotDied;
            types[d] = type;
            (team == 0 ? Allies : Enemies).Add(d);
            return d;
        }

        public RobotType TypeOf(Damageable d) => d != null && types.TryGetValue(d, out var t) ? t : RobotType.Titan;

        /// <summary>Clears a wreck off the field (respawns).</summary>
        public void RemoveWreck(Damageable d)
        {
            if (d == null) return;
            Allies.Remove(d);
            Enemies.Remove(d);
            types.Remove(d);
            Destroy(d.gameObject);
        }

        /// <summary>Rebuilds fallen teammates at the base (between survival waves).</summary>
        public void ReviveAllies()
        {
            for (int i = Allies.Count - 1; i >= 0; i--)
            {
                var a = Allies[i];
                if (a == null || !a.IsDead) continue;
                var type = TypeOf(a);
                string name = a.displayName;
                RemoveWreck(a);
                var n = SpawnRobot(type, 0, Layout.allySpawns[(i + 1) % Layout.allySpawns.Length], name);
                if (Player != null) n.GetComponent<AIBrain>().follow = Player.transform;
                n.GetComponent<AIBrain>().startDelay = 0.5f;
            }
        }

        public void Announce(string text, Color color)
        {
            if (hud != null) hud.Announce(text, color);
        }

        void Update()
        {
            if (Current == State.Fighting && Rules != null) Rules.Tick(Time.deltaTime);
        }

        void OnRobotDied(Damageable d)
        {
            if (Current != State.Fighting) return;
            if (d.team == 1 && Player != null && !Player.IsDead && Time.unscaledTime - lastKillSlowMo > 4f
                && Vector3.Distance(d.transform.position, Player.transform.position) < 130f)
            {
                lastKillSlowMo = Time.unscaledTime;
                StartCoroutine(SlowMotion(0.5f));
            }
            if (d.team == 0 && d != Player) Announce(d.displayName + " DOWN", UiKit.Red);
            Rules?.RobotDied(d);
            // Old enemy wrecks are cleared away so long fights don't fill up with scrap.
            if (d.team == 1) StartCoroutine(ClearWreck(d, 25f));
        }

        IEnumerator ClearWreck(Damageable d, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (d != null && d.IsDead && d != Player) RemoveWreck(d);
        }

        /// <summary>Called by the rules when the match is decided.</summary>
        public void EndMatch(bool won, string title, string sub)
        {
            if (Current != State.Fighting) return;
            Current = won ? State.Won : State.Lost;
            StartCoroutine(SlowMotion(won ? 1.6f : 1.4f));
            StartCoroutine(ShowEndAfter(won ? 2.2f : 2.5f, won, title, sub));
        }

        IEnumerator SlowMotion(float realSeconds)
        {
            Time.timeScale = 0.3f;
            Time.fixedDeltaTime = BaseFixedDelta * Time.timeScale;
            yield return new WaitForSecondsRealtime(realSeconds);
            if (!Paused)
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = BaseFixedDelta;
            }
        }

        IEnumerator ShowEndAfter(float realSeconds, bool won, string title, string sub)
        {
            yield return new WaitForSecondsRealtime(realSeconds);
            bool next = won && Rules != null && Rules.IsMission && MatchSetup.MissionIndex + 1 < MatchSetup.Missions.Count;
            hud.ShowEnd(won, title, sub, next);
        }

        /// <summary>Straight into the next mission with the same robot.</summary>
        public void NextMission()
        {
            MatchSetup.MissionIndex++;
            Restart();
        }

        public void SetPaused(bool pause)
        {
            if (Current != State.Fighting && pause) return;
            Paused = pause;
            Time.timeScale = pause ? 0f : 1f;
            Time.fixedDeltaTime = BaseFixedDelta;
            AudioListener.pause = pause;
            if (pause)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>Same robot again, straight into the fight.</summary>
        public void Restart()
        {
            nextScreen = Screen.Redeploy;
            Reload();
        }

        /// <summary>Back to the hangar to pick a robot.</summary>
        public void ChangeRobot()
        {
            nextScreen = Screen.Hangar;
            Reload();
        }

        public void QuitToMenu()
        {
            nextScreen = Screen.Menu;
            Reload();
        }

        bool reloading;

        void Reload()
        {
            if (reloading) return;
            reloading = true;
            Paused = false;
            AudioListener.pause = false;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = BaseFixedDelta;
            StartCoroutine(ReloadNextFrame());
        }

        IEnumerator ReloadNextFrame()
        {
            // Let the "loading" screen draw first so the tap feels instant.
            yield return null;
            yield return null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
