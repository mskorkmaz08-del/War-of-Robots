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
        public enum State { Select, Fighting, Won, Lost }

        public static MatchManager Instance { get; private set; }

        public Transform playerSpawn;
        public Transform[] enemySpawns = new Transform[0];

        public State Current { get; private set; } = State.Select;
        public Damageable Player { get; private set; }
        public readonly List<Damageable> Enemies = new List<Damageable>();

        Hud hud;
        const float BaseFixedDelta = 0.02f;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = BaseFixedDelta;
            Application.targetFrameRate = 60;
        }

        void Start()
        {
            hud = gameObject.AddComponent<Hud>();
            hud.Build(this);
            hud.ShowSelect();
        }

        public void StartMatch(RobotType type)
        {
            if (Current != State.Select) return;
            Current = State.Fighting;
            GameAudio.Play2D(Sfx.UiClick);

            Vector3 pPos = playerSpawn != null ? playerSpawn.position : new Vector3(0f, 0f, -60f);
            Quaternion pRot = playerSpawn != null ? playerSpawn.rotation : Quaternion.identity;
            var playerGo = RobotFactory.Build(type, pPos, pRot, 0, Livery.Player, true, type == RobotType.Titan ? "TITAN" : "RAPTOR");
            var input = playerGo.AddComponent<RobotInput>();
            hud.ConnectControls(input);
            Player = playerGo.GetComponent<Damageable>();
            Player.Died += OnPlayerDied;

            var cam = FollowCamera.Main;
            if (cam != null)
            {
                if (type == RobotType.Titan) cam.SetTarget(playerGo.transform, 32f, -1f, 2.5f);
                else cam.SetTarget(playerGo.transform, 23f, 5.5f, 2.5f);
            }

            // Enemies: one of each kind, so both styles fight you.
            var enemyTypes = new[] { RobotType.Titan, RobotType.Raptor, RobotType.Raptor };
            var liveries = new[] { Livery.Enemy, Livery.Enemy2, Livery.Enemy };
            var names = new[] { "GOLIATH", "VIPER", "HAVOC" };
            int count = Mathf.Min(2, enemySpawns.Length > 0 ? enemySpawns.Length : 2);
            for (int i = 0; i < count; i++)
            {
                Vector3 ePos = enemySpawns.Length > i ? enemySpawns[i].position : new Vector3((i - 0.5f) * 60f, 0f, 60f);
                Quaternion eRot = enemySpawns.Length > i ? enemySpawns[i].rotation : Quaternion.Euler(0f, 180f, 0f);
                var e = RobotFactory.Build(enemyTypes[i], ePos, eRot, 1, liveries[i], false, names[i]);
                var brain = e.AddComponent<AIBrain>();
                if (enemyTypes[i] == RobotType.Titan)
                {
                    brain.preferredRange = 70f;
                    brain.fireRange = 190f;
                    brain.specialRange = 160f;
                    brain.aggression = 0.5f;
                }
                else
                {
                    brain.preferredRange = 45f;
                    brain.fireRange = 150f;
                    brain.specialRange = 180f;
                    brain.aggression = 0.7f;
                    brain.usesBoostToCharge = true;
                }
                var d = e.GetComponent<Damageable>();
                d.Died += OnEnemyDied;
                Enemies.Add(d);
            }

            hud.ShowFight();
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
            Time.timeScale = 1f;
            Time.fixedDeltaTime = BaseFixedDelta;
        }

        IEnumerator ShowEndAfter(float realSeconds, bool won)
        {
            yield return new WaitForSecondsRealtime(realSeconds);
            hud.ShowEnd(won);
        }

        public void Restart()
        {
            GameAudio.Play2D(Sfx.UiClick);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = BaseFixedDelta;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
