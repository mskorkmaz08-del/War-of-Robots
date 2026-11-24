using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Builds particle effects in code: explosions, sparks, dust, muzzle flashes,
    /// thruster flames and smoke. No prefabs or textures are needed.
    /// </summary>
    public static class Fx
    {
        static readonly Color Fire1 = new Color(1f, 0.85f, 0.45f);
        static readonly Color Fire2 = new Color(1f, 0.45f, 0.1f);
        static readonly Color Fire3 = new Color(0.5f, 0.08f, 0.02f);

        // ---------- One-shot effects ----------

        public static void Explosion(Vector3 pos, float size)
        {
            var assets = GameAssets.Get();
            if (assets == null) return;

            // Fireball
            var fire = NewSystem("Fireball", pos, Quaternion.identity, assets.ParticleAdditive());
            var m = fire.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(2f * size, 7f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(2.5f * size, 5f * size);
            m.startColor = new ParticleSystem.MinMaxGradient(Fire1, Fire2);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.15f;
            SphereShape(fire, 0.6f * size);
            Burst(fire, 26);
            ColorFade(fire, Fire1, Fire2, Fire3);
            SizeCurve(fire, 0.5f, 1.4f);
            Drag(fire, 3f);
            Play(fire, 1.2f);

            // Smoke column
            var smoke = NewSystem("Smoke", pos, Quaternion.identity, assets.ParticleAlpha());
            m = smoke.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1f * size, 4f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(3f * size, 6f * size);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.12f, 0.11f, 0.1f, 0.75f), new Color(0.3f, 0.28f, 0.26f, 0.6f));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.08f;
            SphereShape(smoke, 1f * size);
            Burst(smoke, 18);
            AlphaFade(smoke, new Color(0.2f, 0.19f, 0.18f));
            SizeCurve(smoke, 0.6f, 2.2f);
            Drag(smoke, 1.5f);
            Play(smoke, 5f);

            // Sparks and glowing debris
            Sparks(pos, Vector3.up, Mathf.RoundToInt(30 * Mathf.Clamp(size, 0.5f, 3f)), 25f * Mathf.Sqrt(size), 180f);

            // Ground scorch dust
            Dust(pos, size * 1.2f);

            Flash(pos, new Color(1f, 0.6f, 0.25f), 25f * size, 8f, 0.35f);
        }

        public static void Sparks(Vector3 pos, Vector3 normal, int count = 20, float speed = 20f, float spreadDegrees = 60f)
        {
            var assets = GameAssets.Get();
            if (assets == null) return;
            var ps = NewSystem("Sparks", pos, Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up), assets.ParticleAdditive());
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.6f), new Color(1f, 0.55f, 0.15f));
            m.gravityModifier = 2.2f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = Mathf.Clamp(spreadDegrees * 0.5f, 1f, 89f);
            shape.radius = 0.2f;
            Burst(ps, count);
            ColorFade(ps, Fire1, Fire2, Fire3);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f;
            r.lengthScale = 1.5f;
            Play(ps, 1f);
        }

        public static void Dust(Vector3 pos, float size)
        {
            var assets = GameAssets.Get();
            if (assets == null) return;
            var ps = NewSystem("Dust", pos + Vector3.up * 0.3f, Quaternion.identity, assets.ParticleAlpha());
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(4f * size, 9f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(1.5f * size, 3.5f * size);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.42f, 0.38f, 0.55f), new Color(0.3f, 0.28f, 0.25f, 0.45f));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = 0.05f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.8f * size;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.radiusThickness = 0f;
            Burst(ps, 16);
            AlphaFade(ps, new Color(0.4f, 0.37f, 0.33f));
            SizeCurve(ps, 0.7f, 2f);
            Drag(ps, 4f);
            Play(ps, 3f);
        }

        public static void MuzzleFlash(Vector3 pos, Vector3 dir, float size)
        {
            var assets = GameAssets.Get();
            if (assets == null) return;
            var ps = NewSystem("Muzzle", pos, Quaternion.LookRotation(dir), assets.ParticleAdditive());
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(5f * size, 25f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(1f * size, 2.5f * size);
            m.startColor = new ParticleSystem.MinMaxGradient(Fire1, Fire2);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.1f;
            Burst(ps, 14);
            ColorFade(ps, Fire1, Fire2, Fire3);
            Play(ps, 0.4f);

            var smoke = NewSystem("MuzzleSmoke", pos, Quaternion.LookRotation(dir), assets.ParticleAlpha());
            m = smoke.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 8f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(1f * size, 2.5f * size);
            m.startColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);
            var s2 = smoke.shape;
            s2.enabled = true;
            s2.shapeType = ParticleSystemShapeType.Cone;
            s2.angle = 20f;
            Burst(smoke, 8);
            AlphaFade(smoke, new Color(0.55f, 0.55f, 0.55f));
            SizeCurve(smoke, 0.6f, 2f);
            Drag(smoke, 3f);
            Play(smoke, 2f);

            if (size >= 1.5f) Flash(pos, new Color(1f, 0.7f, 0.35f), 12f * size, 5f, 0.08f);
        }

        public static void EnergyBurst(Vector3 pos, Color color, float size)
        {
            var assets = GameAssets.Get();
            if (assets == null) return;
            var ps = NewSystem("Energy", pos, Quaternion.identity, assets.ParticleAdditive());
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(3f * size, 14f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(0.6f * size, 1.6f * size);
            m.startColor = new ParticleSystem.MinMaxGradient(Color.white, color);
            SphereShape(ps, 0.3f * size);
            Burst(ps, 24);
            ColorFade(ps, Color.white, color, color * 0.3f);
            Drag(ps, 2f);
            Play(ps, 0.8f);
            if (size >= 1f) Flash(pos, color, 15f * size, 6f, 0.15f);
        }

        /// <summary>A short-lived point light. Kept rare and brief for mobile.</summary>
        public static void Flash(Vector3 pos, Color color, float range, float intensity, float duration)
        {
            var go = new GameObject("Flash");
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            var fade = go.AddComponent<LightFade>();
            fade.duration = duration;
        }

        // ---------- Continuous effects ----------

        /// <summary>Rocket flame attached to a nozzle. Control strength with SetEmission.</summary>
        public static ParticleSystem ThrusterFlame(Transform nozzle, float size, Color core)
        {
            var assets = GameAssets.Get();
            if (assets == null) return null;
            var ps = NewSystem("Thruster", nozzle.position, nozzle.rotation, assets.ParticleAdditive(), nozzle);
            var m = ps.main;
            m.loop = true;
            m.duration = 1f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(18f * size, 30f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(0.8f * size, 1.6f * size);
            m.startColor = new ParticleSystem.MinMaxGradient(Color.white, core);
            m.maxParticles = 300;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.35f * size;
            ColorFade(ps, Color.white, core, new Color(0.3f, 0.05f, 0.02f));
            SizeCurve(ps, 1f, 0.2f);
            SetRate(ps, 0f);
            ps.Play();
            return ps;
        }

        /// <summary>Trailing smoke for missiles and damaged robots.</summary>
        public static ParticleSystem SmokeTrail(Transform parent, float size, Color color)
        {
            var assets = GameAssets.Get();
            if (assets == null) return null;
            var ps = NewSystem("SmokeTrail", parent.position, parent.rotation, assets.ParticleAlpha(), parent);
            var m = ps.main;
            m.loop = true;
            m.duration = 1f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.8f * size, 1.6f * size);
            m.startColor = color;
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.05f;
            m.maxParticles = 200;
            AlphaFade(ps, color);
            SizeCurve(ps, 0.6f, 2.5f);
            SetRate(ps, 40f);
            ps.Play();
            return ps;
        }

        /// <summary>Stops a continuous effect, lets its particles fade, then removes it.</summary>
        public static void Detach(ParticleSystem ps, float linger = 3f)
        {
            if (ps == null) return;
            ps.transform.SetParent(null, true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(ps.gameObject, linger);
        }

        public static void SetRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var e = ps.emission;
            e.rateOverTime = rate;
        }

        // ---------- Helpers ----------

        static ParticleSystem NewSystem(string name, Vector3 pos, Quaternion rot, Material mat, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(pos, rot);
            if (parent != null) go.transform.SetParent(parent, true);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = false;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static void Burst(ParticleSystem ps, int count)
        {
            var e = ps.emission;
            e.enabled = true;
            e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, 200)) });
        }

        static void SphereShape(ParticleSystem ps, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.01f, radius);
        }

        static void Drag(ParticleSystem ps, float drag)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
            limit.limit = 1000f;
        }

        static void ColorFade(ParticleSystem ps, Color a, Color b, Color c)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.3f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        static void AlphaFade(ParticleSystem ps, Color color)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        static void SizeCurve(ParticleSystem ps, float start, float end)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, start, 1f, end));
        }

        static void Play(ParticleSystem ps, float lifetime)
        {
            ps.Play();
            Object.Destroy(ps.gameObject, lifetime + 0.5f);
        }
    }

    /// <summary>Fades a light out and removes it.</summary>
    public class LightFade : MonoBehaviour
    {
        public float duration = 0.2f;
        Light lightComp;
        float startIntensity;
        float t;

        void Start()
        {
            lightComp = GetComponent<Light>();
            startIntensity = lightComp.intensity;
        }

        void Update()
        {
            t += Time.deltaTime;
            if (lightComp != null) lightComp.intensity = Mathf.Lerp(startIntensity, 0f, t / duration);
            if (t >= duration) Destroy(gameObject);
        }
    }
}
