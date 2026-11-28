using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    public enum Sfx
    {
        Footstep,
        Servo,
        Cannon,
        Explosion,
        BigExplosion,
        MissileLaunch,
        Laser,
        RailCharge,
        RailFire,
        MetalHit,
        Landing,
        UiClick,
        Debris,
        Whoosh,
        Alarm,
        LockOn,
        Victory,
        Defeat,
        HitTaken,
        Sizzle,
        Confirm,
        Gatling,
        Tesla,
        Hiss,
        FlameStart
    }

    /// <summary>
    /// Plays recorded sound effects (CC0 recordings from Freesound, see CREDITS.md) stored
    /// in Resources/Audio. Each effect has a few variations picked at random so repeated
    /// shots and footsteps don't sound identical. One-shots go through a pool of 3D sources.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const int PoolSize = 28;

        static GameAudio instance;

        // Which files make up each effect. Empty = deliberately silent.
        static readonly Dictionary<Sfx, string[]> Files = new Dictionary<Sfx, string[]>
        {
            { Sfx.Footstep, new[] { "footstep_0", "footstep_1", "footstep_2", "footstep_3", "footstep_4" } },
            { Sfx.Servo, new[] { "servo_0", "servo_1" } },
            { Sfx.Cannon, new[] { "cannon_0", "cannon_1", "cannon_2", "cannon_3" } },
            { Sfx.Explosion, new[] { "explosion_0", "explosion_1", "explosion_2" } },
            { Sfx.BigExplosion, new[] { "bigexplosion_0", "bigexplosion_1" } },
            { Sfx.MissileLaunch, new[] { "missile_0", "missile_1" } },
            { Sfx.Laser, new[] { "laser_0", "laser_1" } },
            { Sfx.RailCharge, new[] { "railcharge_0" } },
            { Sfx.RailFire, new[] { "railfire_0", "railfire_1" } },
            { Sfx.MetalHit, new[] { "metalhit_0", "metalhit_1", "metalhit_2", "metalhit_3" } },
            { Sfx.Landing, new[] { "landing_0", "landing_1" } },
            { Sfx.UiClick, new[] { "ui_click" } },
            { Sfx.Debris, new[] { "debris_0", "debris_1", "debris_2" } },
            { Sfx.Whoosh, new[] { "boost_0" } },
            { Sfx.Alarm, new[] { "alarm_0" } }, // generated warning beep (Tools/make_alarm.py), used when a robot is about to blow
            { Sfx.LockOn, new string[0] },
            { Sfx.Victory, new[] { "victory" } },
            { Sfx.Defeat, new[] { "defeat" } },
            { Sfx.HitTaken, new[] { "metalhit_0", "metalhit_1", "metalhit_3" } },
            { Sfx.Sizzle, new[] { "zap_0", "zap_1" } },
            { Sfx.Confirm, new[] { "deploy_boom" } },
            { Sfx.Gatling, new string[0] }, // the gatling uses a continuous loop instead
            { Sfx.Tesla, new[] { "tesla_0" } },
            { Sfx.Hiss, new[] { "hiss_0" } },
            { Sfx.FlameStart, new[] { "flame_start" } },
        };

        readonly Dictionary<Sfx, AudioClip[]> clips = new Dictionary<Sfx, AudioClip[]>();
        static readonly Dictionary<string, AudioClip> loops = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        bool prewarming;
        AudioSource uiSource;
        AudioSource musicSource;
        AudioSource ambienceSource;
        float musicTarget;
        float ambienceTarget;
        int next;

        public static GameAudio Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("GameAudio");
                    // Survives "fight again" scene reloads so sounds are only loaded once.
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<GameAudio>();
                    instance.Init();
                }
                return instance;
            }
        }

        void Init()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject("Sfx " + i);
                child.transform.SetParent(transform, false);
                var src = child.AddComponent<AudioSource>();
                Configure3D(src, 30f, 520f);
                src.playOnAwake = false;
                pool.Add(src);
            }

            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.spatialBlend = 0f;
            uiSource.playOnAwake = false;
            uiSource.ignoreListenerPause = true;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.spatialBlend = 0f;
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.ignoreListenerPause = true;
            musicSource.volume = 0f;

            ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.spatialBlend = 0f;
            ambienceSource.loop = true;
            ambienceSource.playOnAwake = false;
            ambienceSource.volume = 0f;

            Clips(Sfx.UiClick);
        }

        static AudioClip Load(string name)
        {
            var c = Resources.Load<AudioClip>("Audio/" + name);
            if (c == null) Debug.LogWarning("Missing sound: " + name);
            return c;
        }

        AudioClip[] Clips(Sfx sfx)
        {
            if (clips.TryGetValue(sfx, out var list)) return list;
            var names = Files.TryGetValue(sfx, out var n) ? n : new string[0];
            var loaded = new List<AudioClip>();
            foreach (var name in names)
            {
                var c = Load(name);
                if (c != null) loaded.Add(c);
            }
            list = loaded.ToArray();
            clips[sfx] = list;
            return list;
        }

        AudioClip Pick(Sfx sfx)
        {
            var list = Clips(sfx);
            return list.Length == 0 ? null : list[Random.Range(0, list.Length)];
        }

        static AudioClip Loop(string name)
        {
            if (!loops.TryGetValue(name, out var c) || c == null)
            {
                c = Load(name);
                loops[name] = c;
            }
            return c;
        }

        /// <summary>Loads the sound bank a little at a time (one effect per frame).</summary>
        public static void Prewarm()
        {
            var a = Instance;
            if (!a.prewarming) a.StartCoroutine(a.PrewarmRoutine());
        }

        System.Collections.IEnumerator PrewarmRoutine()
        {
            prewarming = true;
            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx)))
            {
                if (!clips.ContainsKey(sfx))
                {
                    Clips(sfx);
                    yield return null;
                }
            }
            foreach (var l in new[] { "engine_loop", "thrust_loop", "flame_loop", "gatling_loop", "ambience_battle", "music_menu" })
            {
                Loop(l);
                yield return null;
            }
        }

        /// <summary>Menu music (a recorded dark action loop). Level 0 fades it out.</summary>
        public static void PlayMusic(float level = 1f)
        {
            var a = Instance;
            a.musicTarget = level;
            if (level <= 0f) return;
            var clip = Loop("music_menu");
            if (clip == null) return;
            if (a.musicSource.clip != clip) a.musicSource.clip = clip;
            if (!a.musicSource.isPlaying) a.musicSource.Play();
        }

        public static void StopMusic() => PlayMusic(0f);

        /// <summary>Distant battle and wind in the arena.</summary>
        public static void PlayAmbience(bool on)
        {
            var a = Instance;
            a.ambienceTarget = on ? 0.32f : 0f;
            if (!on) return;
            var clip = Loop("ambience_battle");
            if (clip == null) return;
            a.ambienceSource.clip = clip;
            if (!a.ambienceSource.isPlaying) a.ambienceSource.Play();
        }

        void Update()
        {
            if (musicSource == null) return;
            float dt = Time.unscaledDeltaTime;
            float target = musicTarget * GameSettings.MusicVolume * 0.45f;
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, dt * 0.6f);
            if (musicSource.isPlaying && target <= 0f && musicSource.volume <= 0.001f) musicSource.Stop();
            ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, ambienceTarget, dt * 0.4f);
            if (ambienceSource.isPlaying && ambienceTarget <= 0f && ambienceSource.volume <= 0.001f) ambienceSource.Stop();
        }

        public static void Configure3D(AudioSource src, float minDistance, float maxDistance)
        {
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.dopplerLevel = 0.2f;
        }

        /// <summary>Plays a one-shot sound at a world position.</summary>
        public static void Play(Sfx sfx, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            var a = Instance;
            var clip = a.Pick(sfx);
            if (clip == null) return;
            var src = a.NextSource();
            src.transform.position = position;
            // Recordings sound wrong when pitched far from their original, so keep the shift gentle.
            src.pitch = Mathf.Clamp(Mathf.Lerp(1f, pitch, 0.5f), 0.75f, 1.35f) * Random.Range(0.95f, 1.05f);
            src.volume = Mathf.Clamp01(volume);
            src.clip = clip;
            src.Play();

            // Big blasts get a long low rumble layered underneath.
            if (sfx == Sfx.BigExplosion)
            {
                var rumble = Loop("bigexplosion_2");
                if (rumble != null)
                {
                    var r = a.NextSource();
                    r.transform.position = position;
                    r.pitch = 1f;
                    r.volume = Mathf.Clamp01(volume * 0.7f);
                    r.clip = rumble;
                    r.Play();
                }
            }
        }

        AudioSource NextSource()
        {
            // Prefer a free source; otherwise steal the oldest.
            for (int i = 0; i < pool.Count; i++)
            {
                var s = pool[(next + i) % pool.Count];
                if (!s.isPlaying) { next = (next + i + 1) % pool.Count; return s; }
            }
            var src = pool[next];
            next = (next + 1) % pool.Count;
            return src;
        }

        /// <summary>Non-positional sound (UI, stingers).</summary>
        public static void Play2D(Sfx sfx, float volume = 1f)
        {
            var a = Instance;
            var clip = a.Pick(sfx);
            if (clip == null) return;
            a.uiSource.PlayOneShot(clip, volume);
        }

        /// <summary>Looping diesel engine for the wheeled robots and walker reactors.</summary>
        public static AudioClip EngineLoop() { _ = Instance; return Loop("engine_loop"); }

        /// <summary>Looping jet roar for boosters and jump jets.</summary>
        public static AudioClip ThrustLoop() { _ = Instance; return Loop("thrust_loop"); }

        /// <summary>Looping roaring fire for the flamethrower.</summary>
        public static AudioClip FlameLoop() { _ = Instance; return Loop("flame_loop"); }

        /// <summary>Looping minigun fire.</summary>
        public static AudioClip GatlingLoop() { _ = Instance; return Loop("gatling_loop"); }
    }
}
