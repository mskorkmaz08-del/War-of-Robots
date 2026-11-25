using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Reuses particle systems and flash lights instead of creating and destroying
    /// them for every shot, footstep and explosion. Creating objects mid-fight is
    /// what made the game hitch on phones and in the browser.
    /// Also caps how many copies of one effect can run at once.
    /// </summary>
    public class FxPool : MonoBehaviour
    {
        const int MaxActivePerEffect = 24;
        const int MaxFlashes = 3;

        static FxPool instance;

        readonly Dictionary<string, Stack<ParticleSystem>> free = new Dictionary<string, Stack<ParticleSystem>>();
        readonly Dictionary<string, int> active = new Dictionary<string, int>();
        readonly Dictionary<ParticleSystem, string> keys = new Dictionary<ParticleSystem, string>();
        readonly List<(ParticleSystem ps, float time)> pending = new List<(ParticleSystem, float)>();
        readonly List<LightFade> flashes = new List<LightFade>();

        static FxPool Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("FxPool");
                    instance = go.AddComponent<FxPool>();
                }
                return instance;
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Gets a stopped, cleared one-shot system. False when too many are already playing.</summary>
        public static bool TryAcquire(string key, out ParticleSystem ps, out bool isNew)
        {
            var pool = Instance;
            pool.active.TryGetValue(key, out int count);
            if (count >= MaxActivePerEffect)
            {
                ps = null;
                isNew = false;
                return false;
            }
            ps = pool.Take(key, out isNew);
            pool.active[key] = count + 1;
            return true;
        }

        /// <summary>Gets a system for a looping effect (thrusters, smoke trails). Never refused.</summary>
        public static ParticleSystem AcquireContinuous(string key)
        {
            var pool = Instance;
            var ps = pool.Take(key, out _);
            pool.active.TryGetValue(key, out int count);
            pool.active[key] = count + 1;
            return ps;
        }

        ParticleSystem Take(string key, out bool isNew)
        {
            if (free.TryGetValue(key, out var stack))
            {
                while (stack.Count > 0)
                {
                    var ps = stack.Pop();
                    if (ps == null) continue;
                    ps.gameObject.SetActive(true);
                    isNew = false;
                    return ps;
                }
            }
            var created = Fx.CreateSystem(key);
            created.transform.SetParent(transform, false);
            keys[created] = key;
            isNew = true;
            return created;
        }

        /// <summary>Stops emitting now and returns the system to the pool after a delay.</summary>
        public static void Release(ParticleSystem ps, float delay)
        {
            if (ps == null) return;
            var pool = Instance;
            if (!pool.keys.ContainsKey(ps))
            {
                Destroy(ps.gameObject, delay);
                return;
            }
            if (ps.transform.parent != pool.transform) ps.transform.SetParent(pool.transform, true);
            if (ps.main.loop) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            pool.pending.Add((ps, Time.time + delay));
        }

        void Update()
        {
            float now = Time.time;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var (ps, time) = pending[i];
                if (ps != null && time > now) continue;
                pending.RemoveAt(i);
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
                string key = keys[ps];
                if (!free.TryGetValue(key, out var stack))
                {
                    stack = new Stack<ParticleSystem>();
                    free[key] = stack;
                }
                stack.Push(ps);
                if (active.TryGetValue(key, out int count)) active[key] = Mathf.Max(0, count - 1);
            }
        }

        /// <summary>A free flash light, or null when the few allowed are all lit.</summary>
        public static LightFade AcquireFlash()
        {
            var pool = Instance;
            foreach (var f in pool.flashes)
                if (f != null && !f.Busy) return f;
            if (pool.flashes.Count >= MaxFlashes) return null;
            var go = new GameObject("Flash");
            go.transform.SetParent(pool.transform, false);
            var fade = go.AddComponent<LightFade>();
            fade.Init();
            pool.flashes.Add(fade);
            return fade;
        }

        /// <summary>
        /// Plays every effect once out of sight while the menu is open, so the
        /// pools are filled and the shaders are compiled before the first fight.
        /// </summary>
        public static void Prewarm(Vector3 hiddenPoint)
        {
            Fx.Explosion(hiddenPoint, 1f);
            Fx.Explosion(hiddenPoint, 2f);
            Fx.MuzzleFlash(hiddenPoint, Vector3.forward, 1f);
            Fx.EnergyBurst(hiddenPoint, Color.cyan, 1f);
            Fx.Dust(hiddenPoint, 1f);
            Fx.Dust(hiddenPoint, 1f);
            for (int i = 0; i < 4; i++) Fx.Sparks(hiddenPoint, Vector3.up);
        }
    }
}
