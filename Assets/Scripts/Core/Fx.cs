using System.Collections.Generic;
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

            // Where the ground is, so the dust skirt and debris sit on it.
            float groundY = pos.y - 1f;
            if (Physics.Raycast(pos + Vector3.up * 1f, Vector3.down, out RaycastHit gh, 12f + size * 3f, ~(1 << 2), QueryTriggerInteraction.Ignore) && gh.rigidbody == null)
                groundY = gh.point.y;
            bool nearGround = pos.y - groundY < 6f * size;

            // Everything grows from one volume: the fireball turns into the smoke cloud,
            // which is fed from the same spot, so the blast reads as one solid thing.

            // 1. White-hot core: a few glowing puffs that swell and vanish at once.
            if (FxPool.TryAcquire("Core", out var core, out bool isNew))
            {
                Place(core, pos, Quaternion.identity);
                if (isNew)
                {
                    Setup(core, assets.FireGlow());
                    Billows(core);
                    ColorFade(core, Color.white, Core, Fire2);
                    SizeCurve(core, 0.5f, 1.4f);
                }
                var cm = core.main;
                cm.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
                cm.startSpeed = new ParticleSystem.MinMaxCurve(0f, 2f * size);
                cm.startSize = new ParticleSystem.MinMaxCurve(4.5f * size, 7f * size);
                cm.startColor = Core;
                cm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                SphereShape(core, 0.6f * size);
                Burst(core, 5);
                Play(core, 0.4f);
            }

            // 2. Fireball: rolling flame puffs that cool from yellow-white through orange
            //    to soot, and keep rising as the start of the smoke cloud.
            if (!FxPool.TryAcquire("Fireball", out var fire, out isNew)) return;
            Place(fire, pos, Quaternion.identity);
            if (isNew) FireballSetup(fire, assets);
            var m = fire.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(2f * size, 9f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(2.4f * size, 4.6f * size);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.8f), new Color(1f, 0.75f, 0.4f));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.35f;
            SphereShape(fire, 0.9f * size);
            Burst(fire, high ? 34 : 18);
            Play(fire, 2f);

            // 3. Burning debris: glowing streaks that arc out and fall.
            if (FxPool.TryAcquire("Embers", out var embers, out isNew))
            {
                Place(embers, pos, Quaternion.identity);
                if (isNew)
                {
                    Setup(embers, assets.ParticleAdditive());
                    ColorFade(embers, Fire1, Fire2, Fire3);
                    var er = embers.GetComponent<ParticleSystemRenderer>();
                    er.renderMode = ParticleSystemRenderMode.Stretch;
                    er.velocityScale = 0.05f;
                    er.lengthScale = 1.5f;
                }
                var em = embers.main;
                em.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 2f);
                em.startSpeed = new ParticleSystem.MinMaxCurve(10f * Mathf.Sqrt(size), 30f * Mathf.Sqrt(size));
                em.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                em.startColor = new ParticleSystem.MinMaxGradient(Fire1, Fire2);
                em.gravityModifier = 1.6f;
                var es = embers.shape;
                es.enabled = true;
                es.shapeType = ParticleSystemShapeType.Hemisphere;
                es.radius = 0.8f * size;
                es.rotation = new Vector3(-90f, 0f, 0f);
                Burst(embers, high ? 26 : 12);
                Play(embers, 2.2f);
            }

            // 4. Real 3D wreckage: lit, tumbling metal shards that land on the ground.
            if (FxPool.TryAcquire("Shards", out var shards, out isNew))
            {
                Place(shards, pos, Quaternion.identity);
                if (isNew)
                {
                    Setup(shards, assets.Burnt());
                    var sr = shards.GetComponent<ParticleSystemRenderer>();
                    sr.renderMode = ParticleSystemRenderMode.Mesh;
                    sr.mesh = ShardMesh();
                    sr.alignment = ParticleSystemRenderSpace.World;
                    sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    sr.receiveShadows = true;
                    var km0 = shards.main;
                    km0.startRotation3D = true;
                    km0.startSize3D = true;
                    var rot = shards.rotationOverLifetime;
                    rot.enabled = true;
                    rot.separateAxes = true;
                    rot.x = new ParticleSystem.MinMaxCurve(-9f, 9f);
                    rot.y = new ParticleSystem.MinMaxCurve(-9f, 9f);
                    rot.z = new ParticleSystem.MinMaxCurve(-9f, 9f);
                    var col = shards.collision;
                    col.enabled = true;
                    col.type = ParticleSystemCollisionType.World;
                    col.mode = ParticleSystemCollisionMode.Collision3D;
                    col.quality = ParticleSystemCollisionQuality.Low;
                    col.bounce = 0.25f;
                    col.dampen = 0.5f;
                    col.lifetimeLoss = 0f;
                    col.collidesWith = ~(1 << 2);
                    var alpha = shards.colorOverLifetime;
                    alpha.enabled = false;
                    var shrink = shards.sizeOverLifetime;
                    shrink.enabled = true;
                    shrink.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));
                }
                var km = shards.main;
                km.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
                km.startSpeed = new ParticleSystem.MinMaxCurve(7f * Mathf.Sqrt(size), 20f * Mathf.Sqrt(size));
                km.startSizeX = new ParticleSystem.MinMaxCurve(0.4f * size, 1.1f * size);
                km.startSizeY = new ParticleSystem.MinMaxCurve(0.15f * size, 0.5f * size);
                km.startSizeZ = new ParticleSystem.MinMaxCurve(0.3f * size, 0.9f * size);
                km.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                km.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                km.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                km.startColor = Color.white;
                km.gravityModifier = 2.2f;
                var ks = shards.shape;
                ks.enabled = true;
                ks.shapeType = ParticleSystemShapeType.Hemisphere;
                ks.radius = 0.6f * size;
                ks.rotation = new Vector3(-90f, 0f, 0f);
                Burst(shards, high ? 12 : 6);
                Play(shards, 4.2f);
            }

            // 5. Smoke: fed from inside the fireball a moment later, so the flames sink
            //    into it, then a dark column climbs and spreads.
            if (FxPool.TryAcquire("Smoke", out var smoke, out isNew))
            {
                Place(smoke, pos + Vector3.up * 0.5f * size, Quaternion.identity);
                if (isNew)
                {
                    Setup(smoke, assets.SmokeBillow());
                    Billows(smoke);
                    SmokeFade(smoke);
                    SizeCurve(smoke, 0.55f, 2.4f);
                    Drag(smoke, 1.6f);
                    Spin(smoke, 0.5f);
                }
                m = smoke.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * size, 5f * size);
                m.startSize = new ParticleSystem.MinMaxCurve(3.5f * size, 6.5f * size);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.15f, 0.14f, 0.9f), new Color(0.32f, 0.3f, 0.28f, 0.8f));
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.gravityModifier = -0.14f;
                SphereShape(smoke, 1.1f * size);
                Burst(smoke, high ? 20 : 11, 0.12f);
                Play(smoke, 7f);
            }

            // 6. Dust skirt kicked out along the ground by the blast.
            if (nearGround) Dust(new Vector3(pos.x, groundY, pos.z), size * 1.3f);

            Sparks(pos, Vector3.up, Mathf.RoundToInt(22 * Mathf.Clamp(size, 0.5f, 3f)), 26f * Mathf.Sqrt(size), 180f);
            Scorch(pos, size);
            Flash(pos, new Color(1f, 0.62f, 0.28f), 30f * size, 9f, 0.5f);

            // Big blasts keep going: secondary bursts inside the same cloud.
            if (size >= 2f && high)
                CoroutineHost.Run(Aftershocks(pos, size));
        }

        static void FireballSetup(ParticleSystem fire, GameAssets assets)
        {
            Setup(fire, assets.FireBillow());
            Billows(fire);
            Drag(fire, 3f);
            Spin(fire, 0.8f);
            SizeCurve(fire, 0.45f, 1.9f);
            // Cools as it rises: white-hot, orange, deep red, then soot that fades out.
            var col = fire.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.18f),
                        new GradientColorKey(new Color(0.45f, 0.12f, 0.04f), 0.4f), new GradientColorKey(new Color(0.11f, 0.1f, 0.09f), 0.65f),
                        new GradientColorKey(new Color(0.16f, 0.15f, 0.14f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0.7f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        static System.Collections.IEnumerator Aftershocks(Vector3 pos, float size)
        {
            int n = size >= 3f ? 3 : 2;
            for (int i = 0; i < n; i++)
            {
                yield return new WaitForSeconds(Random.Range(0.15f, 0.35f));
                Vector3 p = pos + Vector3.Scale(Random.insideUnitSphere, new Vector3(1f, 0.6f, 1f)) * 1.3f * size;
                SmallBlast(p, size * 0.5f);
                GameAudio.Play(Sfx.Explosion, p, 0.7f, Random.Range(1f, 1.2f));
            }
        }

        /// <summary>A quick fireball and sparks, no smoke column (follow-up blasts, hits).</summary>
        public static void SmallBlast(Vector3 pos, float size)
        {
            var assets = GameAssets.Get();
            if (assets == null || !FxPool.TryAcquire("Fireball", out var fire, out bool isNew)) return;
            Place(fire, pos, Quaternion.identity);
            if (isNew) FireballSetup(fire, assets);
            var m = fire.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(2f * size, 7f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(2.2f * size, 4.2f * size);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.8f), new Color(1f, 0.75f, 0.4f));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.3f;
            SphereShape(fire, 0.6f * size);
            Burst(fire, 14);
            Play(fire, 1.5f);
            Sparks(pos, Vector3.up, 12, 18f, 160f);
        }

        /// <summary>Use the 4x4 puff sheet: each particle gets one random puff for its whole life.</summary>
        static void Billows(ParticleSystem ps)
        {
            var assets = GameAssets.Get();
            var ts = ps.textureSheetAnimation;
            if (assets == null || !assets.HasBillows) { ts.enabled = false; return; }
            ts.enabled = true;
            ts.mode = ParticleSystemAnimationMode.Grid;
            ts.numTilesX = 4;
            ts.numTilesY = 4;
            ts.animation = ParticleSystemAnimationType.WholeSheet;
            ts.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            ts.cycleCount = 1;
        }

        /// <summary>Slow random roll so puffs churn instead of sliding.</summary>
        static void Spin(ParticleSystem ps, float radiansPerSecond)
        {
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-radiansPerSecond, radiansPerSecond);
        }

        /// <summary>Smoke that thickens quickly, then thins out slowly.</summary>
        static void SmokeFade(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.6f, 0.45f, 0.35f), 0f), new GradientColorKey(Color.white, 0.25f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        static Mesh shardMesh;

        /// <summary>A jagged, bent plate of torn armour for debris particles.</summary>
        static Mesh ShardMesh()
        {
            if (shardMesh != null) return shardMesh;
            var v = new[]
            {
                new Vector3(-0.5f, 0.1f, -0.35f), new Vector3(0.1f, 0.15f, -0.5f), new Vector3(0.55f, 0f, -0.1f),
                new Vector3(0.3f, -0.05f, 0.45f), new Vector3(-0.2f, 0.05f, 0.5f), new Vector3(-0.55f, 0.2f, 0.1f),
            };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            // Top and bottom faces of a thick, bent plate, plus the torn edge.
            Vector3 down = new Vector3(0f, -0.28f, 0f);
            int n = v.Length;
            for (int i = 0; i < n; i++) verts.Add(v[i]);
            for (int i = 0; i < n; i++) verts.Add(v[i] + down + new Vector3(0f, Mathf.Sin(i * 1.7f) * 0.05f, 0f));
            for (int i = 1; i < n - 1; i++) { tris.Add(0); tris.Add(i + 1); tris.Add(i); tris.Add(n); tris.Add(n + i); tris.Add(n + i + 1); }
            for (int i = 0; i < n; i++)
            {
                int a = i, b = (i + 1) % n;
                tris.Add(a); tris.Add(b); tris.Add(n + b);
                tris.Add(a); tris.Add(n + b); tris.Add(n + a);
            }
            // Flat-shade: unshare vertices so the torn edges catch the light.
            var fv = new Vector3[tris.Count];
            var ft = new int[tris.Count];
            for (int i = 0; i < tris.Count; i++) { fv[i] = verts[tris[i]]; ft[i] = i; }
            shardMesh = new Mesh { name = "Shard", vertices = fv, triangles = ft };
            shardMesh.RecalculateNormals();
            shardMesh.RecalculateBounds();
            return shardMesh;
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
                Setup(ps, assets.SmokeBillow());
                Billows(ps);
                AlphaFade(ps, new Color(0.4f, 0.37f, 0.33f));
                SizeCurve(ps, 0.6f, 2.2f);
                Drag(ps, 4f);
                Spin(ps, 0.4f);
            }
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(5f * size, 12f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(1.6f * size, 3.4f * size);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.5f, 0.44f, 0.75f), new Color(0.38f, 0.35f, 0.31f, 0.6f));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = 0.05f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.8f * size;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.radiusThickness = 0f;
            Burst(ps, 20);
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
                Setup(smoke, assets.SmokeBillow());
                Billows(smoke);
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
            Setup(ps, assets.SmokeBillow());
            Billows(ps);
            Spin(ps, 0.5f);
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

        static void Burst(ParticleSystem ps, int count, float delay = 0f)
        {
            var e = ps.emission;
            e.enabled = true;
            OneBurst[0] = new ParticleSystem.Burst(delay, (short)Mathf.Clamp(count, 1, 200));
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
