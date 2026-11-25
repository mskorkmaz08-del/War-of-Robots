using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotArena
{
    /// <summary>
    /// Runs a match: robot selection, spawning the player and AI enemies,
    /// detecting victory or defeat, slow-motion finishing blows and restarting.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public enum State { Menu, Hangar, Deploying, Fighting, Won, Lost }
        public enum Screen { Menu, Hangar, Redeploy }

        public static MatchManager Instance { get; private set; }

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

        public void BackToMenu()
        {
            if (Current != State.Hangar) return;
            Current = State.Menu;
            showroom.Close();
            hud.ShowMenu();
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

        /// <summary>Builds one robot per frame so the screen never freezes on a tap.</summary>
        IEnumerator Deploy(RobotType type)
        {
            // Let the "deploying" screen draw before the heavy work starts.
            yield return null;
            yield return null;

            Vector3 pPos = playerSpawn != null ? playerSpawn.position : new Vector3(0f, 0f, -60f);
            Quaternion pRot = playerSpawn != null ? playerSpawn.rotation : Quaternion.identity;
            var playerGo = RobotFactory.Build(type, pPos, pRot, 0, Livery.Player, true, RobotCatalog.Get(type).name);
            var input = playerGo.AddComponent<RobotInput>();
            hud.ConnectControls(input);
            var playerTurret = playerGo.GetComponent<Turret>();
            if (playerTurret != null) playerTurret.aimCamera = FollowCamera.Main;
            Player = playerGo.GetComponent<Damageable>();
            Player.Died += OnPlayerDied;

            var cam = FollowCamera.Main;
            if (cam != null)
            {
                var pi = RobotCatalog.Get(type);
                cam.SetTarget(playerGo.transform, pi.camDistance, pi.camHeight, pi.camPivot);
            }
            yield return null;

            // Enemies: two different robots picked at random from the catalog.
            var pool = new List<RobotInfo>(RobotCatalog.All);
            var liveries = new[] { Livery.Enemy, Livery.Enemy2, Livery.Enemy };
            var namePool = new List<string> { "GOLIATH", "VIPER", "HAVOC", "MAULER", "WRAITH", "KRAKEN", "BRUTUS", "STINGER", "OGRE", "VENOM" };
            int count = Mathf.Min(2, enemySpawns.Length > 0 ? enemySpawns.Length : 2);
            for (int i = 0; i < count; i++)
            {
                var info = pool[Random.Range(0, pool.Count)];
                pool.Remove(info);
                string enemyName = namePool[Random.Range(0, namePool.Count)];
                namePool.Remove(enemyName);
                Vector3 ePos = enemySpawns.Length > i ? enemySpawns[i].position : new Vector3((i - 0.5f) * 60f, 0f, 60f);
                Quaternion eRot = enemySpawns.Length > i ? enemySpawns[i].rotation : Quaternion.Euler(0f, 180f, 0f);
                var e = RobotFactory.Build(info.type, ePos, eRot, 1, liveries[i], false, enemyName);
                var brain = e.AddComponent<AIBrain>();
                brain.preferredRange = info.aiPreferredRange;
                brain.fireRange = info.aiFireRange;
                brain.specialRange = info.aiSpecialRange;
                brain.aggression = info.aiAggression;
                brain.usesBoostToCharge = info.aiBoostCharge;
                var d = e.GetComponent<Damageable>();
                d.Died += OnEnemyDied;
                Enemies.Add(d);
                yield return null;
            }

            Current = State.Fighting;
            hud.ShowFight();
            GameAudio.Prewarm();
        }

        void OnEnemyDied(Damageable d)
        {
            if (Current != State.Fighting) return;
            bool allDead = true;
            foreach (var e in Enemies) if (e != null && !e.IsDead) allDead = false;
            if (allDead)
            {
                Current = State.Won;
                StartCoroutine(SlowMotion(1.6f));
                StartCoroutine(ShowEndAfter(2.2f, true));
            }
            else
            {
                StartCoroutine(SlowMotion(0.6f));
            }
        }

        void OnPlayerDied(Damageable d)
        {
            if (Current != State.Fighting) return;
            Current = State.Lost;
            StartCoroutine(SlowMotion(1.4f));
            StartCoroutine(ShowEndAfter(2.5f, false));
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

        IEnumerator ShowEndAfter(float realSeconds, bool won)
        {
            yield return new WaitForSecondsRealtime(realSeconds);
            hud.ShowEnd(won);
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
