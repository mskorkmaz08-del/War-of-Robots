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
        Gatling
    }

    /// <summary>
    /// All sound effects are synthesised in code when the game starts (no audio files),
    /// then played through a small pool of 3D audio sources.
    /// Big, low and slow sounds with long falloff help the robots feel huge.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const int SampleRate = 22050;
        const int PoolSize = 24;

        static GameAudio instance;
        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        readonly Dictionary<Sfx, System.Func<AudioClip>> recipes = new Dictionary<Sfx, System.Func<AudioClip>>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        bool prewarming;
        AudioSource uiSource;
        AudioSource musicSource;
        AudioSource ambienceSource;
        static AudioClip musicClip;
        static AudioClip ambienceClip;
        float musicTarget;
        int next;

        static AudioClip engineLoop;
        static AudioClip thrustLoop;

        public static GameAudio Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("GameAudio");
                    // Survives "fight again" scene reloads so sounds are only made once.
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
                Configure3D(src, 25f, 450f);
                src.playOnAwake = false;
                pool.Add(src);
            }

            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.spatialBlend = 0f;
            uiSource.playOnAwake = false;
            uiSource.ignoreListenerPause = true;

            // Sounds are synthesised lazily (or ahead of time by Prewarm) instead of all at once,
            // so tapping a button never waits for the whole sound bank.
            recipes[Sfx.Footstep] = () => Make("Footstep", 0.9f, Footstep);
            recipes[Sfx.Servo] = () => Make("Servo", 0.35f, Servo);
            recipes[Sfx.Cannon] = () => Make("Cannon", 1.4f, Cannon);
            recipes[Sfx.Explosion] = () => Make("Explosion", 2.2f, t => Explosion(t, 2.2f, 0.9f));
            recipes[Sfx.BigExplosion] = () => Make("BigExplosion", 4f, t => Explosion(t, 4f, 1.3f));
            recipes[Sfx.MissileLaunch] = () => Make("MissileLaunch", 1.1f, MissileLaunch);
            recipes[Sfx.Laser] = () => Make("Laser", 0.28f, Laser);
            recipes[Sfx.RailCharge] = () => Make("RailCharge", 1.5f, RailCharge);
            recipes[Sfx.RailFire] = () => Make("RailFire", 2.2f, RailFire);
            recipes[Sfx.MetalHit] = () => Make("MetalHit", 0.8f, MetalHit);
            recipes[Sfx.Landing] = () => Make("Landing", 2f, Landing);
            recipes[Sfx.UiClick] = () => Make("UiClick", 0.08f, t => Mathf.Sin(t * 2f * Mathf.PI * 1200f) * (1f - t / 0.08f) * 0.4f);
            recipes[Sfx.Debris] = () => Make("Debris", 2.2f, Debris);
            recipes[Sfx.Whoosh] = () => Make("Whoosh", 0.7f, Whoosh);
            recipes[Sfx.Alarm] = () => Make("Alarm", 0.5f, Alarm);
            recipes[Sfx.LockOn] = () => Make("LockOn", 0.22f, LockOn);
            recipes[Sfx.Victory] = () => Make("Victory", 2.6f, Victory);
            recipes[Sfx.Defeat] = () => Make("Defeat", 2.8f, Defeat);
            recipes[Sfx.HitTaken] = () => Make("HitTaken", 0.7f, HitTaken);
            recipes[Sfx.Sizzle] = () => Make("Sizzle", 0.35f, Sizzle);
            recipes[Sfx.Confirm] = () => Make("Confirm", 0.35f, Confirm);
            recipes[Sfx.Gatling] = () => Make("Gatling", 0.16f, Gatling);
            clips[Sfx.UiClick] = recipes[Sfx.UiClick]();

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.spatialBlend = 0f;
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.ignoreListenerPause = true;
            ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.spatialBlend = 0f;
            ambienceSource.loop = true;
            ambienceSource.playOnAwake = false;
        }

        AudioClip Clip(Sfx sfx)
        {
            if (clips.TryGetValue(sfx, out var clip)) return clip;
            if (!recipes.TryGetValue(sfx, out var recipe)) return null;
            clip = recipe();
            clips[sfx] = clip;
            return clip;
        }

        /// <summary>Synthesises the sound bank a little at a time (one sound per frame) while the menu is open.</summary>
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
                    Clip(sfx);
                    yield return null;
                }
            }
            EngineLoop();
            yield return null;
            ThrustLoop();
            yield return null;
            if (musicClip == null) musicClip = Make("Music", MusicLength, Music, false);
            yield return null;
            if (ambienceClip == null) ambienceClip = Make("Ambience", 6f, Ambience, true);
        }

        /// <summary>Starts (or keeps) the background music. Volume follows the settings.</summary>
        public static void PlayMusic(float level = 1f)
        {
            var a = Instance;
            if (musicClip == null) musicClip = Make("Music", MusicLength, Music, false);
            a.musicTarget = level;
            if (a.musicSource.clip != musicClip) a.musicSource.clip = musicClip;
            if (!a.musicSource.isPlaying) a.musicSource.Play();
        }

        /// <summary>Wind and distant machinery in the arena.</summary>
        public static void PlayAmbience(bool on)
        {
            var a = Instance;
            if (on)
            {
                if (ambienceClip == null) ambienceClip = Make("Ambience", 6f, Ambience, true);
                a.ambienceSource.clip = ambienceClip;
                a.ambienceSource.volume = 0.35f;
                if (!a.ambienceSource.isPlaying) a.ambienceSource.Play();
            }
            else a.ambienceSource.Stop();
        }

        void Update()
        {
            if (musicSource == null) return;
            float target = musicTarget * GameSettings.MusicVolume * 0.5f;
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, Time.unscaledDeltaTime * 0.5f);
        }

        public static void Configure3D(AudioSource src, float minDistance, float maxDistance)
        {
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.dopplerLevel = 0.3f;
        }

        /// <summary>Plays a one-shot sound at a world position.</summary>
        public static void Play(Sfx sfx, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            var a = Instance;
            var clip = a.Clip(sfx);
            if (clip == null) return;
            var src = a.pool[a.next];
            a.next = (a.next + 1) % a.pool.Count;
            src.transform.position = position;
            src.pitch = pitch * Random.Range(0.94f, 1.06f);
            src.volume = Mathf.Clamp01(volume);
            src.clip = clip;
            src.Play();
        }

        /// <summary>Non-positional sound (UI).</summary>
        public static void Play2D(Sfx sfx, float volume = 1f)
        {
            var a = Instance;
            var clip = a.Clip(sfx);
            if (clip == null) return;
            a.uiSource.PlayOneShot(clip, volume);
        }

        /// <summary>Looping heavy engine rumble.</summary>
        public static AudioClip EngineLoop()
        {
            if (engineLoop == null)
            {
                _ = Instance;
                engineLoop = Make("EngineLoop", 2f, EngineHum, true);
            }
            return engineLoop;
        }

        /// <summary>Looping rocket roar for boosters and jump jets.</summary>
        public static AudioClip ThrustLoop()
        {
            if (thrustLoop == null)
            {
                _ = Instance;
                thrustLoop = Make("ThrustLoop", 2f, Thrust, true);
            }
            return thrustLoop;
        }

        // ---------- Synthesis ----------

        static AudioClip Make(string name, float seconds, System.Func<float, float> gen, bool loop = false)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            ResetNoise();
            float peak = 0.0001f;
            for (int i = 0; i < count; i++)
            {
                float v = gen(i / (float)SampleRate);
                data[i] = v;
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }

            float norm = 0.9f / peak;
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(data[i] * norm, -1f, 1f);

            if (loop)
            {
                // Crossfade the ends so the loop is seamless.
                int fade = SampleRate / 10;
                for (int i = 0; i < fade; i++)
                {
                    float w = i / (float)fade;
                    data[i] = data[i] * w + data[count - fade + i] * (1f - w);
                }
                System.Array.Resize(ref data, count - fade);
                count -= fade;
            }

            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Deterministic noise with simple low-pass filters.
        static uint noiseState = 1;
        static float lp1, lp2, lp3;

        static void ResetNoise()
        {
            noiseState = 2463534242;
            lp1 = lp2 = lp3 = 0f;
        }

        static float Noise()
        {
            noiseState ^= noiseState << 13;
            noiseState ^= noiseState >> 17;
            noiseState ^= noiseState << 5;
            return (noiseState / (float)uint.MaxValue) * 2f - 1f;
        }

        static float LowPass(ref float state, float input, float cutoffHz)
        {
            float rc = 1f / (2f * Mathf.PI * Mathf.Max(10f, cutoffHz));
            float dt = 1f / SampleRate;
            float a = dt / (rc + dt);
            state += a * (input - state);
            return state;
        }

        static float Env(float t, float attack, float decay)
        {
            if (t < attack) return t / attack;
            return Mathf.Exp(-(t - attack) / decay);
        }

        static float Sine(float t, float f) => Mathf.Sin(2f * Mathf.PI * f * t);

        static float Footstep(float t)
        {
            float pitchDrop = Mathf.Lerp(70f, 32f, Mathf.Clamp01(t / 0.4f));
            float body = Mathf.Sin(2f * Mathf.PI * pitchDrop * t) * Env(t, 0.005f, 0.22f);
            float crunch = LowPass(ref lp1, Noise(), 700f) * Env(t, 0.002f, 0.06f) * 0.9f;
            float rattle = LowPass(ref lp2, Noise(), 2500f) * Env(t, 0.03f, 0.12f) * 0.15f;
            return body * 1.2f + crunch + rattle;
        }

        static float Servo(float t)
        {
            float f = Mathf.Lerp(380f, 620f, t / 0.35f);
            float tone = (Sine(t, f) + 0.4f * Sine(t, f * 2.01f)) * 0.3f;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.35f));
            return tone * env;
        }

        static float Cannon(float t)
        {
            float crack = Noise() * Env(t, 0.001f, 0.03f);
            float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 35f, Mathf.Clamp01(t / 0.5f)) * t) * Env(t, 0.003f, 0.35f);
            float rumble = LowPass(ref lp1, Noise(), 300f) * Env(t, 0.01f, 0.5f) * 2.5f;
            return crack * 0.8f + boom + rumble;
        }

        static float Explosion(float t, float length, float weight)
        {
            float cutoff = Mathf.Lerp(2500f, 150f, Mathf.Clamp01(t / (length * 0.6f)));
            float noise = LowPass(ref lp1, Noise(), cutoff);
            noise = LowPass(ref lp2, noise, cutoff * 1.5f);
            float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(60f, 25f, Mathf.Clamp01(t / 1f)) * t) * Env(t, 0.005f, 0.6f * weight);
            float crackle = (Noise() > 0.995f ? Noise() : 0f) * Env(t, 0.1f, 0.6f);
            return noise * Env(t, 0.004f, 0.55f * weight) * 3f + boom * 1.1f + crackle * 0.4f;
        }

        static float MissileLaunch(float t)
        {
            float cutoff = Mathf.Lerp(600f, 3000f, Mathf.Clamp01(t / 0.3f));
            float hiss = LowPass(ref lp1, Noise(), cutoff) * Env(t, 0.02f, 0.4f);
            float pop = Mathf.Sin(2f * Mathf.PI * 120f * t) * Env(t, 0.001f, 0.05f);
            return hiss * 2f + pop * 0.6f;
        }

        static float Laser(float t)
        {
            float f = Mathf.Lerp(1800f, 300f, t / 0.28f);
            float zap = Mathf.Sign(Sine(t, f)) * 0.3f + Sine(t, f * 0.5f) * 0.5f;
            return zap * Env(t, 0.002f, 0.07f);
        }

        static float RailCharge(float t)
        {
            float f = Mathf.Lerp(120f, 1400f, (t / 1.5f) * (t / 1.5f));
            float tone = Sine(t, f) * 0.6f + Sine(t, f * 1.5f) * 0.25f;
            float crackle = LowPass(ref lp1, Noise(), 4000f) * 0.2f * (t / 1.5f);
            return (tone + crackle) * Mathf.Clamp01(t / 0.1f);
        }

        static float RailFire(float t)
        {
            float zap = Mathf.Sign(Sine(t, Mathf.Lerp(900f, 80f, Mathf.Clamp01(t / 0.6f)))) * Env(t, 0.001f, 0.15f) * 0.6f;
            float boom = Explosion(t, 2.2f, 1.1f) * 0.5f;
            return zap + boom;
        }

        static float MetalHit(float t)
        {
            float clang = Sine(t, 523f) + 0.7f * Sine(t, 1307f) + 0.5f * Sine(t, 2011f) + 0.3f * Sine(t, 3220f);
            float thump = Sine(t, 80f) * Env(t, 0.002f, 0.08f) * 2f;
            return clang * Env(t, 0.001f, 0.18f) * 0.4f + thump + Noise() * Env(t, 0.0005f, 0.015f);
        }

        static float Landing(float t)
        {
            return Footstep(t) * 1.3f + Explosion(t, 2f, 0.7f) * 0.5f;
        }

        static float Debris(float t)
        {
            // Metal bits clattering down after a blast.
            float hits = 0f;
            for (int k = 0; k < 7; k++)
            {
                float start = 0.15f + k * 0.21f + Mathf.Sin(k * 12.9898f) * 0.08f;
                float lt = t - start;
                if (lt < 0f) continue;
                float f = 700f + 900f * Mathf.Abs(Mathf.Sin(k * 78.233f));
                hits += (Sine(lt, f) + 0.6f * Sine(lt, f * 2.4f)) * Env(lt, 0.001f, 0.05f) * Mathf.Exp(-k * 0.25f);
            }
            float rumble = LowPass(ref lp1, Noise(), 250f) * Env(t, 0.01f, 0.6f) * 1.5f;
            return hits * 0.35f + rumble;
        }

        static float Whoosh(float t)
        {
            float k = t / 0.7f;
            float cutoff = Mathf.Lerp(300f, 3500f, Mathf.Sin(k * Mathf.PI));
            return LowPass(ref lp1, Noise(), cutoff) * Mathf.Sin(k * Mathf.PI) * 2.2f;
        }

        static float Alarm(float t)
        {
            float on = t < 0.18f || (t > 0.25f && t < 0.43f) ? 1f : 0f;
            return (Mathf.Sign(Sine(t, 880f)) * 0.25f + Sine(t, 880f) * 0.4f) * on;
        }

        static float LockOn(float t)
        {
            float f = t < 0.1f ? 1400f : 2100f;
            return Sine(t, f) * 0.5f * (t < 0.08f || t > 0.11f ? 1f : 0f) * Env(t, 0.002f, 0.12f);
        }

        static float Note(float t, float start, float len, float f)
        {
            float lt = t - start;
            if (lt < 0f || lt > len + 0.6f) return 0f;
            float env = Mathf.Min(1f, lt / 0.01f) * (lt < len ? 1f : Mathf.Exp(-(lt - len) / 0.15f));
            float saw = ((lt * f) % 1f) * 2f - 1f;
            return (Sine(lt, f) * 0.6f + saw * 0.25f + Sine(lt, f * 2f) * 0.15f) * env;
        }

        static float Victory(float t)
        {
            // Rising brass-like fanfare: C E G C'.
            float v = Note(t, 0f, 0.18f, 261.6f) + Note(t, 0.2f, 0.18f, 329.6f) + Note(t, 0.4f, 0.18f, 392f) + Note(t, 0.6f, 1.2f, 523.3f);
            v += Note(t, 0.6f, 1.2f, 392f) * 0.6f + Note(t, 0.6f, 1.2f, 329.6f) * 0.5f;
            float boom = Sine(t, 55f) * Env(t, 0.005f, 0.4f) * 0.8f;
            return v * 0.5f + boom;
        }

        static float Defeat(float t)
        {
            // Falling minor phrase with a low rumble.
            float v = Note(t, 0f, 0.35f, 392f) + Note(t, 0.4f, 0.35f, 349.2f) + Note(t, 0.8f, 0.35f, 311.1f) + Note(t, 1.2f, 1.2f, 261.6f);
            v += Note(t, 1.2f, 1.2f, 155.6f) * 0.7f;
            float rumble = LowPass(ref lp1, Noise(), 120f) * Env(t, 0.05f, 1.5f) * 2f;
            return v * 0.45f + rumble;
        }

        static float HitTaken(float t)
        {
            float thud = Sine(t, Mathf.Lerp(110f, 45f, Mathf.Clamp01(t / 0.3f))) * Env(t, 0.002f, 0.18f) * 1.4f;
            float clang = (Sine(t, 410f) + 0.6f * Sine(t, 977f)) * Env(t, 0.001f, 0.12f) * 0.35f;
            float crunch = LowPass(ref lp1, Noise(), 1200f) * Env(t, 0.001f, 0.06f);
            return thud + clang + crunch;
        }

        static float Sizzle(float t)
        {
            float hiss = (LowPass(ref lp1, Noise(), 6000f) - LowPass(ref lp2, Noise(), 1500f)) * 2f;
            return hiss * Env(t, 0.002f, 0.1f) + Sine(t, 2400f) * Env(t, 0.001f, 0.03f) * 0.2f;
        }

        static float Gatling(float t)
        {
            float crack = Noise() * Env(t, 0.0005f, 0.012f);
            float thump = Sine(t, Mathf.Lerp(180f, 70f, Mathf.Clamp01(t / 0.08f))) * Env(t, 0.001f, 0.04f);
            float body = LowPass(ref lp1, Noise(), 900f) * Env(t, 0.001f, 0.05f) * 1.5f;
            return crack * 0.7f + thump + body;
        }

        static float Confirm(float t)
        {
            return (Note(t, 0f, 0.06f, 660f) + Note(t, 0.08f, 0.12f, 990f)) * 0.5f;
        }

        // ---------- Music: a driving 8-bar battle loop ----------

        const float Bpm = 112f;
        static float Beat => 60f / Bpm;
        static float MusicLength => Beat * 32f;
        static readonly float[] BassLine = { 55f, 55f, 65.4f, 49f, 55f, 55f, 73.4f, 65.4f };

        static float Music(float t)
        {
            float beat = Beat;
            float bt = t % beat;                    // time within the beat
            int beatIndex = Mathf.FloorToInt(t / beat);
            int bar = beatIndex / 4;

            // Kick on every beat, snare on 2 and 4, hats on the off-beats.
            float kick = Sine(bt, Mathf.Lerp(120f, 45f, Mathf.Clamp01(bt / 0.12f))) * Env(bt, 0.002f, 0.13f) * 1.3f;
            float snare = (beatIndex % 2 == 1) ? (Noise() * 0.7f + Sine(bt, 190f) * 0.4f) * Env(bt, 0.001f, 0.09f) : 0f;
            float ht = (t + beat * 0.5f) % beat;
            float hat = (Noise() - LowPass(ref lp3, Noise(), 5000f)) * Env(ht, 0.001f, 0.025f) * 0.35f;

            // Pulsing eighth-note bass.
            float root = BassLine[bar % BassLine.Length];
            float et = t % (beat * 0.5f);
            float saw = ((t * root) % 1f) * 2f - 1f;
            float bass = LowPass(ref lp1, saw, 400f + 600f * Env(et, 0.002f, 0.08f)) * Mathf.Min(1f, et / 0.005f) * Env(et, 0.003f, 0.2f) * 1.6f;

            // Dark pad: minor chord over the bass.
            float pad = (Sine(t, root * 2f) + Sine(t, root * 2.378f) + Sine(t, root * 3f)) * 0.08f * (0.7f + 0.3f * Sine(t, 0.25f));

            return kick + snare * 0.6f + hat + bass * 0.7f + pad;
        }

        static float Ambience(float t)
        {
            float wind = LowPass(ref lp1, Noise(), 350f + 150f * Sine(t, 0.17f)) * (0.8f + 0.2f * Sine(t, 0.31f));
            float drone = (Sine(t, 48f) + 0.5f * Sine(t, 72.3f)) * 0.08f;
            return wind * 1.5f + drone;
        }

        static float EngineHum(float t)
        {
            float f = 42f;
            float saw = ((t * f) % 1f) * 2f - 1f;
            float growl = LowPass(ref lp1, saw, 220f) * 1.5f;
            float wobble = 1f + 0.15f * Sine(t, 3f);
            float whine = Sine(t, 310f) * 0.06f;
            return growl * wobble + whine + LowPass(ref lp2, Noise(), 150f) * 0.6f;
        }

        static float Thrust(float t)
        {
            float roar = LowPass(ref lp1, Noise(), 900f) * 1.8f;
            float body = LowPass(ref lp2, Noise(), 200f) * 2.5f;
            return roar + body;
        }
    }
}
