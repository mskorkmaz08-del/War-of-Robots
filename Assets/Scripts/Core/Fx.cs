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

        static readonly Color Core = new Color(1f, 0.97f, 0.85f);
        static readonly Collider[] GroundCheck = new Collider[1];

        /// <summary>
        /// A layered explosion: white-hot flash, rolling fireball, shockwave ring,
        /// flying embers and burning debris, a tall smoke column, a scorch mark,
        /// and for big blasts a few follow-up explosions.
        /// </summary>
        public static void Explosion(Vector3 pos, float size)
        {
            var assets = GameAssets.Get();
            if (assets == null) return;
            bool high = GameSettings.GraphicsQuality != GameSettings.Quality.Low;

            // 1. Flash core: a few huge bright puffs that vanish almost at once.
            if (FxPool.TryAcquire("Core", out var core, out bool isNew))
            {
                Place(core, pos, Quaternion.identity);
                if (isNew)
                {
                    Setup(core, assets.ParticleAdditive());
                    ColorFade(core, Color.white, Core, Fire2);
                    SizeCurve(core, 0.6f, 1.5f);
                }
                var cm = core.main;
                cm.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
                cm.startSpeed = 0f;
                cm.startSize = new ParticleSystem.MinMaxCurve(6f * size, 9f * size);
                cm.startColor = Core;
                SphereShape(core, 0.3f * size);
                Burst(core, 3);
                Play(core, 0.3f);
            }

            // 2. Fireball: billowing flames that rise and darken.
            if (!FxPool.TryAcquire("Fireball", out var fire, out isNew)) return;
            Place(fire, pos, Quaternion.identity);
            if (isNew)
            {
                Setup(fire, assets.ParticleAdditive());
                ColorFade(fire, Core, Fire2, Fire3);
                SizeCurve(fire, 0.4f, 1.6f);
                Drag(fire, 3.5f);
            }
            var m = fire.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(3f * size, 11f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(3f * size, 6.5f * size);
            m.startColor = new ParticleSystem.MinMaxGradient(Fire1, Fire2);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.25f;
            SphereShape(fire, 0.8f * size);
            Burst(fire, high ? 40 : 24);
            Play(fire, 1.5f);

            // 3. Shockwave ring racing along the ground.
            if (FxPool.TryAcquire("Shockwave", out var wave, out isNew))
            {
                Place(wave, pos + Vector3.up * 0.4f, Quaternion.identity);
                if (isNew)
                {
                    Setup(wave, assets.RingAdditive());
                    var wr = wave.GetComponent<ParticleSystemRenderer>();
                    wr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                    SizeCurve(wave, 0.1f, 1f);
                    var col = wave.colorOverLifetime;
                    col.enabled = true;
                    var g = new Gradient();
                    g.SetKeys(new[] { new GradientColorKey(Core, 0f), new GradientColorKey(Fire2, 1f) },
                              new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
                    col.color = new ParticleSystem.MinMaxGradient(g);
                }
                var wm = wave.main;
                wm.startLifetime = 0.45f;
                wm.startSpeed = 0f;
                wm.startSize = 30f * size;
                wm.startColor = Color.white;
                Burst(wave, 1);
                Play(wave, 0.5f);
            }

            // 4. Burning debris: heavy glowing chunks that arc out and fall.
            if (FxPool.TryAcquire("Embers", out var embers, out isNew))
            {
                Place(embers, pos, Quaternion.identity);
                if (isNew)
                {
                    Setup(embers, assets.ParticleAdditive());
                    ColorFade(embers, Fire1, Fire2, Fire3);
                    var er = embers.GetComponent<ParticleSystemRenderer>();
                    er.renderMode = ParticleSystemRenderMode.Stretch;
                    er.velocityScale = 0.04f;
                    er.lengthScale = 1.2f;
                }
                var em = embers.main;
                em.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
                em.startSpeed = new ParticleSystem.MinMaxCurve(10f * Mathf.Sqrt(size), 32f * Mathf.Sqrt(size));
                em.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                em.startColor = new ParticleSystem.MinMaxGradient(Fire1, Fire2);
                em.gravityModifier = 1.6f;
                var es = embers.shape;
                es.enabled = true;
                es.shapeType = ParticleSystemShapeType.Hemisphere;
                es.radius = 0.5f * size;
                es.rotation = new Vector3(-90f, 0f, 0f);
                Burst(embers, high ? 30 : 14);
                Play(embers, 2.8f);
            }

            // 5. Dark chunks of metal thrown out.
            if (FxPool.TryAcquire("Chunks", out var chunks, out isNew))
            {
                Place(chunks, pos, Quaternion.identity);
                if (isNew)
                {
                    Setup(chunks, assets.ParticleAlpha());
                    var rot = chunks.rotationOverLifetime;
                    rot.enabled = true;
                    rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
                }
                var km = chunks.main;
                km.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
                km.startSpeed = new ParticleSystem.MinMaxCurve(8f * Mathf.Sqrt(size), 22f * Mathf.Sqrt(size));
                km.startSize = new ParticleSystem.MinMaxCurve(0.5f * size, 1.3f * size);
                km.startColor = new Color(0.08f, 0.07f, 0.06f, 1f);
                km.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                km.gravityModifier = 2f;
                var ks = chunks.shape;
                ks.enabled = true;
                ks.shapeType = ParticleSystemShapeType.Hemisphere;
                ks.radius = 0.5f * size;
                ks.rotation = new Vector3(-90f, 0f, 0f);
                Burst(chunks, high ? 14 : 6);
                Play(chunks, 2.2f);
            }

            // 6. Smoke column that rises and spreads.
            if (FxPool.TryAcquire("Smoke", out var smoke, out isNew))
            {
                Place(smoke, pos, Quaternion.identity);
                if (isNew)
                {
                    Setup(smoke, assets.ParticleAlpha());
                    AlphaFade(smoke, new Color(0.2f, 0.19f, 0.18f));
                    SizeCurve(smoke, 0.5f, 2.6f);
                    Drag(smoke, 1.5f);
                }
                m = smoke.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * size, 5f * size);
                m.startSize = new ParticleSystem.MinMaxCurve(3.5f * size, 7f * size);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.09f, 0.085f, 0.08f, 0.8f), new Color(0.26f, 0.24f, 0.22f, 0.65f));
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.gravityModifier = -0.12f;
                SphereShape(smoke, 1.2f * size);
                Burst(smoke, high ? 22 : 12);
                Play(smoke, 6.5f);
            }

            Sparks(pos, Vector3.up, Mathf.RoundToInt(36 * Mathf.Clamp(size, 0.5f, 3f)), 28f * Mathf.Sqrt(size), 180f);
            Dust(pos, size * 1.4f);
            Scorch(pos, size);
            Flash(pos, new Color(1f, 0.62f, 0.28f), 30f * size, 9f, 0.4f);

            // Big blasts keep going: a few follow-up explosions around the first.
            if (size >= 2f && high)
                CoroutineHost.Run(Aftershocks(pos, size));
        }

        static System.Collections.IEnumerator Aftershocks(Vector3 pos, float size)
        {
            int n = size >= 3f ? 3 : 2;
            for (int i = 0; i < n; i++)
            {
                yield return new WaitForSeconds(Random.Range(0.12f, 0.3f));
                Vector3 p = pos + Vector3.Scale(Random.insideUnitSphere, new Vector3(1f, 0.5f, 1f)) * 3f * size;
                SmallBlast(p, size * 0.45f);
                GameAudio.Play(Sfx.Explosion, p, 0.7f, Random.Range(1f, 1.2f));
            }
        }

        /// <summary>A quick fireball and sparks, no smoke column (follow-up blasts, hits).</summary>
        public static void SmallBlast(Vector3 pos, float size)
        {
            var assets = GameAssets.Get();
            if (assets == null || !FxPool.TryAcquire("Fireball", out var fire, out bool isNew)) return;
            Place(fire, pos, Quaternion.identity);
            if (isNew)
            {
                Setup(fire, assets.ParticleAdditive());
                ColorFade(fire, Core, Fire2, Fire3);
                SizeCurve(fire, 0.4f, 1.6f);
                Drag(fire, 3.5f);
            }
            var m = fire.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(2f * size, 8f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(2.5f * size, 5f * size);
            m.startColor = new ParticleSystem.MinMaxGradient(Fire1, Fire2);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.25f;
            SphereShape(fire, 0.6f * size);
            Burst(fire, 18);
            Play(fire, 1f);
            Sparks(pos, Vector3.up, 16, 20f, 160f);
        }

        // Scorch marks are pooled quads on the ground that fade out.
        static readonly System.Collections.Generic.List<ScorchMark> scorches = new System.Collections.Generic.List<ScorchMark>();
        static int nextScorch;

        public static void Scorch(Vector3 pos, float size)
        {
            if (!Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f + size * 2f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (hit.rigidbody != null) return;
            var assets = GameAssets.Get();
            if (assets == null) return;

            scorches.RemoveAll(sm => sm == null);
            ScorchMark mark;
            if (scorches.Count < 14)
            {
                var go = new GameObject("Scorch");
                go.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = assets.Scorch();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                mark = go.AddComponent<ScorchMark>();
                scorches.Add(mark);
            }
            else
            {
                mark = scorches[nextScorch % scorches.Count];
                nextScorch++;
            }
            float s = 6f * size * Random.Range(0.85f, 1.2f);
            mark.transform.SetPositionAndRotation(hit.point + hit.normal * 0.05f,
                Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            mark.transform.localScale = new Vector3(s, s, 1f);
            mark.Begin();
        }

        static Mesh quad;

        static Mesh QuadMesh()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "Decal" };
            // Faces along -Z so LookRotation(normal) points it away from the surface toward the camera side.
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            return quad;
        }

        public static void Sparks(Vector3 pos, Vector3 normal, int count = 20, float speed = 20f, float spreadDegrees = 60f)
        {
            var assets = GameAssets.Get();
            if (assets == null) return;
            if (!FxPool.TryAcquire("Sparks", out var ps, out bool isNew)) return;
            Place(ps, pos, Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up));
            if (isNew)
            {
                Setup(ps, assets.ParticleAdditive());
                ColorFade(ps, Fire1, Fire2, Fire3);
                var sr = ps.GetComponent<ParticleSystemRenderer>();
                sr.renderMode = ParticleSystemRenderMode.Stretch;
                sr.velocityScale = 0.06f;
                sr.lengthScale = 1.5f;
            }
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
            Play(ps, 1f);
        }

        public static void Dust(Vector3 pos, float size)
        {
            var assets = GameAssets.Get();
            if (assets == null) return;
            if (!FxPool.TryAcquire("Dust", out var ps, out bool isNew)) return;
            Place(ps, pos + Vector3.up * 0.3f, Quaternion.identity);
            if (isNew)
            {
                Setup(ps, assets.ParticleAlpha());
                AlphaFade(ps, new Color(0.4f, 0.37f, 0.33f));
                SizeCurve(ps, 0.7f, 2f);
                Drag(ps, 4f);
            }
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
            Play(ps, 3f);
        }

        public static void MuzzleFlash(Vector3 pos, Vector3 dir, float size)
        {
            var assets = GameAssets.Get();
            if (assets == null) return;
            if (!FxPool.TryAcquire("Muzzle", out var ps, out bool isNew)) return;
            Place(ps, pos, Quaternion.LookRotation(dir));
            if (isNew)
            {
                Setup(ps, assets.ParticleAdditive());
                ColorFade(ps, Fire1, Fire2, Fire3);
            }
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
            Play(ps, 0.4f);

            if (FxPool.TryAcquire("MuzzleSmoke", out var smoke, out isNew))
            {
            Place(smoke, pos, Quaternion.LookRotation(dir));
            if (isNew)
            {
                Setup(smoke, assets.ParticleAlpha());
                AlphaFade(smoke, new Color(0.55f, 0.55f, 0.55f));
                SizeCurve(smoke, 0.6f, 2f);
                Drag(smoke, 3f);
            }
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
            Play(smoke, 2f);
            }

            if (size >= 1.5f) Flash(pos, new Color(1f, 0.7f, 0.35f), 12f * size, 5f, 0.08f);
        }

        public static void EnergyBurst(Vector3 pos, Color color, float size)
        {
            var assets = GameAssets.Get();
            if (assets == null) return;
            if (!FxPool.TryAcquire("Energy" + ColorUtility.ToHtmlStringRGB(color), out var ps, out bool isNew)) return;
            Place(ps, pos, Quaternion.identity);
            if (isNew)
            {
                Setup(ps, assets.ParticleAdditive());
                ColorFade(ps, Color.white, color, color * 0.3f);
                Drag(ps, 2f);
            }
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(3f * size, 14f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(0.6f * size, 1.6f * size);
            m.startColor = new ParticleSystem.MinMaxGradient(Color.white, color);
            SphereShape(ps, 0.3f * size);
            Burst(ps, 24);
            Play(ps, 0.8f);
            if (size >= 1f) Flash(pos, color, 15f * size, 6f, 0.15f);
        }

        /// <summary>A short-lived point light. Kept rare and brief for mobile.</summary>
        public static void Flash(Vector3 pos, Color color, float range, float intensity, float duration)
        {
            var fade = FxPool.AcquireFlash();
            if (fade == null) return;
            fade.transform.position = pos;
            fade.Begin(color, range, intensity, duration);
        }

        // ---------- Continuous effects ----------

        /// <summary>Rocket flame attached to a nozzle. Control strength with SetEmission.</summary>
        public static ParticleSystem ThrusterFlame(Transform nozzle, float size, Color core)
        {
            var assets = GameAssets.Get();
            if (assets == null) return null;
            var ps = FxPool.AcquireContinuous("Thruster");
            Place(ps, nozzle.position, nozzle.rotation);
            ps.transform.SetParent(nozzle, true);
            Setup(ps, assets.ParticleAdditive());
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
            var ps = FxPool.AcquireContinuous("SmokeTrail");
            Place(ps, parent.position, parent.rotation);
            ps.transform.SetParent(parent, true);
            Setup(ps, assets.ParticleAlpha());
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
            FxPool.Release(ps, linger);
        }

        public static void SetRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var e = ps.emission;
            e.rateOverTime = rate;
        }

        // ---------- Helpers ----------

        internal static ParticleSystem CreateSystem(string name)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            return ps;
        }

        static void Place(ParticleSystem ps, Vector3 pos, Quaternion rot)
        {
            ps.transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>Baseline settings shared by every effect.</summary>
        static void Setup(ParticleSystem ps, Material mat)
        {
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

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        static readonly ParticleSystem.Burst[] OneBurst = new ParticleSystem.Burst[1];

        static void Burst(ParticleSystem ps, int count)
        {
            var e = ps.emission;
            e.enabled = true;
            OneBurst[0] = new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, 200));
            e.SetBursts(OneBurst, 1);
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
            FxPool.Release(ps, lifetime + 0.5f);
        }
    }

    /// <summary>A burn mark that slowly fades away.</summary>
    public class ScorchMark : MonoBehaviour
    {
        const float Life = 14f;
        float age;
        MeshRenderer r;
        MaterialPropertyBlock block;
        static readonly int TintId = Shader.PropertyToID("_TintColor");

        public void Begin()
        {
            age = 0f;
            enabled = true;
            if (r == null) r = GetComponent<MeshRenderer>();
            r.enabled = true;
        }

        void Update()
        {
            age += Time.deltaTime;
            float a = Mathf.Clamp01((Life - age) / 4f);
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor(TintId, new Color(0.5f, 0.5f, 0.5f, 0.5f * a));
            r.SetPropertyBlock(block);
            if (age >= Life)
            {
                r.enabled = false;
                enabled = false;
            }
        }
    }

    /// <summary>Fades a pooled light out, then switches it off for reuse.</summary>
    public class LightFade : MonoBehaviour
    {
        Light lightComp;
        float startIntensity;
        float duration = 0.2f;
        float t;

        public bool Busy => enabled;

        public void Init()
        {
            lightComp = gameObject.AddComponent<Light>();
            lightComp.type = LightType.Point;
            lightComp.shadows = LightShadows.None;
            lightComp.enabled = false;
            enabled = false;
        }

        public void Begin(Color color, float range, float intensity, float seconds)
        {
            lightComp.color = color;
            lightComp.range = range;
            lightComp.intensity = intensity;
            lightComp.enabled = true;
            startIntensity = intensity;
            duration = Mathf.Max(0.01f, seconds);
            t = 0f;
            enabled = true;
        }

        void Update()
        {
            t += Time.deltaTime;
            lightComp.intensity = Mathf.Lerp(startIntensity, 0f, t / duration);
            if (t >= duration)
            {
                lightComp.enabled = false;
                enabled = false;
            }
        }
    }
}
