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
        UiClick
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
            clips[Sfx.UiClick] = recipes[Sfx.UiClick]();
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
