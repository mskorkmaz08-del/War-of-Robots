using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// The busy industrial dressing of the hangar bay: a repair frame around the robot
    /// with welding arms throwing sparks, chains and hooks hanging from the roof,
    /// two tiers of catwalks along the back wall, pipe runs, a giant ventilation fan,
    /// glowing control screens and shafts of light through the haze.
    /// </summary>
    public partial class Showroom
    {
        void BuildDetails(Material darkMat, Material steelMat, Material plateMat, Material hazard, Material coldLamp)
        {
            var a = GameAssets.Get();
            var screen = a.Glow(new Color(0.25f, 0.75f, 0.9f), 1.3f);
            var screenDim = a.Glow(new Color(0.15f, 0.45f, 0.55f), 1f);
            var redLamp = a.Glow(new Color(1f, 0.2f, 0.12f), 2f);
            var chainMat = a.Paint(new Color(0.2f, 0.2f, 0.21f), 0.85f, 0.45f, false);
            var pipeMat = a.Paint(new Color(0.24f, 0.26f, 0.27f), 0.7f, 0.4f, true);
            var greenPipe = a.Paint(new Color(0.18f, 0.25f, 0.2f), 0.5f, 0.35f, true);

            // ---- Repair frame: two scaffold towers and a bridge over the robot ----
            const float towerX = 21f, towerZ0 = -3f, towerZ1 = 11f, towerH = 34f;
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * towerX;
                foreach (float dx in new[] { -2.5f, 2.5f })
                    foreach (float z in new[] { towerZ0, towerZ1 })
                        Part(Shape.Box, new Vector3(x + dx, towerH * 0.5f, z), Vector3.zero, new Vector3(0.8f, towerH, 0.8f), darkMat);
                for (float y = 10f; y < towerH; y += 10f)
                {
                    // Deck with a grated surface and a railing on the robot side
                    Part(Shape.Box, new Vector3(x, y, (towerZ0 + towerZ1) * 0.5f), Vector3.zero, new Vector3(6f, 0.35f, towerZ1 - towerZ0 + 1f), steelMat);
                    for (int k = 0; k < 6; k++)
                        Part(Shape.Box, new Vector3(x, y + 0.19f, towerZ0 + 1f + k * 2.2f), Vector3.zero, new Vector3(5.6f, 0.04f, 0.12f), darkMat);
                    float rx = x - sx * 3f;
                    Part(Shape.Box, new Vector3(rx, y + 1.1f, (towerZ0 + towerZ1) * 0.5f), Vector3.zero, new Vector3(0.12f, 0.12f, towerZ1 - towerZ0), hazard);
                    Part(Shape.Box, new Vector3(rx, y + 0.6f, (towerZ0 + towerZ1) * 0.5f), Vector3.zero, new Vector3(0.08f, 0.08f, towerZ1 - towerZ0), darkMat);
                    for (float z = towerZ0; z <= towerZ1 + 0.1f; z += 3.5f)
                        Part(Shape.Box, new Vector3(rx, y + 0.6f, z), Vector3.zero, new Vector3(0.1f, 1.1f, 0.1f), darkMat);
                    // X-bracing on the outer face
                    foreach (float r in new[] { 58f, -58f })
                        Part(Shape.Box, new Vector3(x + sx * 2.5f, y - 5f, (towerZ0 + towerZ1) * 0.5f), new Vector3(r, 0f, 0f), new Vector3(0.3f, 11.5f, 0.3f), steelMat);
                    // Warning lamp at each deck corner
                    Part(Shape.Box, new Vector3(rx, y + 1.35f, towerZ0), Vector3.zero, new Vector3(0.35f, 0.35f, 0.35f), redLamp);
                }
                // Stair flights zig-zagging up the back
                for (float y = 0f; y < towerH - 4f; y += 10f)
                    Part(Shape.Box, new Vector3(x + sx * 0.2f, y + 5f, towerZ1 + 2.2f), new Vector3(0f, 0f, sx * 55f), new Vector3(1.4f, 12f, 1.6f), steelMat);
                // Cable bundles down the columns
                Hose(new[] { new Vector3(x - sx * 2.5f, towerH, towerZ0 - 0.6f), new Vector3(x - sx * 2.9f, towerH * 0.6f, towerZ0 - 0.9f), new Vector3(x - sx * 2.7f, 6f, towerZ0 - 0.7f), new Vector3(x - sx * 6f, 0.3f, towerZ0 - 2f) }, 0.22f, darkMat);
                Hose(new[] { new Vector3(x - sx * 2.2f, towerH, towerZ0 - 0.8f), new Vector3(x - sx * 2.4f, towerH * 0.5f, towerZ0 - 1.2f), new Vector3(x - sx * 2.3f, 5f, towerZ0 - 1f), new Vector3(x - sx * 4f, 0.2f, towerZ0 - 3.5f) }, 0.14f, greenPipe);

                // Welding arm on the middle deck, reaching for the robot
                Vector3 baseP = new Vector3(x - sx * 2f, 20.4f, 1.5f);
                Part(Shape.Cylinder, baseP + Vector3.up * 0.5f, Vector3.zero, new Vector3(2.2f, 1f, 2.2f), darkMat);
                Vector3 shoulder = baseP + Vector3.up * 1.6f;
                Vector3 elbow = shoulder + new Vector3(-sx * 3.2f, 3.4f, -0.6f);
                Vector3 tip = elbow + new Vector3(-sx * 4.2f, -3.6f, -1.2f);
                Beam(shoulder, elbow, 0.9f, hazard);
                Beam(elbow, tip, 0.7f, hazard);
                Part(Shape.Cylinder, shoulder, new Vector3(90f, 0f, 0f), new Vector3(1.5f, 1.2f, 1.5f), darkMat);
                Part(Shape.Cylinder, elbow, new Vector3(90f, 0f, 0f), new Vector3(1.2f, 1.1f, 1.2f), darkMat);
                Part(Shape.Box, tip, Quaternion.LookRotation(tip - elbow).eulerAngles, new Vector3(0.6f, 0.6f, 1.6f), darkMat);
                Hose(new[] { shoulder + new Vector3(0f, -0.6f, 0.6f), (shoulder + elbow) * 0.5f + new Vector3(0f, -0.8f, 0.7f), elbow + new Vector3(0f, -0.5f, 0.5f), tip + new Vector3(0f, 0.3f, 0.4f) }, 0.12f, darkMat);
                Vector3 torch = tip + (tip - elbow).normalized * 0.9f;
                Welder(transform.TransformPoint(torch), sx);
            }
            // Bridge girder across the top, with a hoist and a hanging hook
            Part(Shape.Box, new Vector3(0f, towerH + 0.8f, (towerZ0 + towerZ1) * 0.5f), Vector3.zero, new Vector3(towerX * 2f + 6f, 1.6f, 2.2f), hazard);
            for (float x = -towerX + 3f; x < towerX - 2f; x += 3f)
                Part(Shape.Box, new Vector3(x, towerH + 0.8f, (towerZ0 + towerZ1) * 0.5f - 1.15f), new Vector3(0f, 0f, 45f), new Vector3(0.25f, 2f, 0.1f), darkMat);
            Part(Shape.Box, new Vector3(6f, towerH - 0.6f, 4f), Vector3.zero, new Vector3(3f, 1.4f, 2.6f), darkMat);
            Chain(new Vector3(6f, towerH - 1.3f, 4f), 9f, chainMat, darkMat);

            // ---- Chains and hooks hanging from the roof trusses ----
            Chain(new Vector3(-36f, 49f, 22f), 24f, chainMat, darkMat);
            Chain(new Vector3(-31f, 49f, 30f), 17f, chainMat, darkMat);
            Chain(new Vector3(37f, 49f, 18f), 21f, chainMat, darkMat);
            Chain(new Vector3(-12f, 49f, 44f), 13f, chainMat, darkMat);
            Chain(new Vector3(15f, 49f, 50f), 27f, chainMat, darkMat);

            // ---- Two tiers of catwalks along the back wall ----
            const float back = 70f;
            foreach (float y in new[] { 12f, 24f })
            {
                Part(Shape.Box, new Vector3(0f, y, back - 5f), Vector3.zero, new Vector3(124f, 0.45f, 6f), steelMat);
                Part(Shape.Box, new Vector3(0f, y - 0.6f, back - 2.1f), Vector3.zero, new Vector3(124f, 0.8f, 0.4f), darkMat);
                Part(Shape.Box, new Vector3(0f, y + 1.15f, back - 8f), Vector3.zero, new Vector3(124f, 0.14f, 0.14f), hazard);
                Part(Shape.Box, new Vector3(0f, y + 0.6f, back - 8f), Vector3.zero, new Vector3(124f, 0.08f, 0.08f), darkMat);
                for (float x = -60f; x <= 60f; x += 4f)
                    Part(Shape.Box, new Vector3(x, y + 0.6f, back - 8f), Vector3.zero, new Vector3(0.1f, 1.2f, 0.1f), darkMat);
                for (float x = -56f; x <= 56f; x += 16f)
                {
                    Part(Shape.Box, new Vector3(x, y * 0.5f, back - 7.6f), Vector3.zero, new Vector3(0.6f, y, 0.6f), darkMat);
                    Part(Shape.Box, new Vector3(x + 2.5f, y - 2f, back - 7.6f), new Vector3(0f, 0f, 45f), new Vector3(0.3f, 5f, 0.3f), steelMat);
                }
                // Control consoles with glowing screens
                for (float x = -48f; x <= 48f; x += 24f)
                {
                    Part(Shape.Box, new Vector3(x, y + 0.9f, back - 3f), new Vector3(-15f, 0f, 0f), new Vector3(3.2f, 1.4f, 1.2f), darkMat);
                    Part(Shape.Box, new Vector3(x, y + 1.0f, back - 3.62f), new Vector3(-15f, 0f, 0f), new Vector3(2.7f, 1.0f, 0.05f), (int)x % 48 == 0 ? screen : screenDim);
                    Part(Shape.Box, new Vector3(x + 0.2f, y + 3.6f, back - 2.3f), Vector3.zero, new Vector3(4.5f, 2.6f, 0.2f), darkMat);
                    Part(Shape.Box, new Vector3(x + 0.2f, y + 3.6f, back - 2.42f), Vector3.zero, new Vector3(4.1f, 2.2f, 0.05f), screenDim);
                }
            }
            // Stairs between the tiers
            Part(Shape.Box, new Vector3(-44f, 18f, back - 5f), new Vector3(0f, 0f, 50f), new Vector3(1.6f, 15.5f, 3.5f), steelMat);
            Part(Shape.Box, new Vector3(44f, 6f, back - 5f), new Vector3(0f, 0f, -50f), new Vector3(1.6f, 15.5f, 3.5f), steelMat);

            // ---- Pipe runs along the back wall and big vertical mains ----
            foreach (var (y, r, mat) in new[] { (30f, 0.9f, pipeMat), (31.9f, 0.6f, greenPipe), (33.4f, 0.7f, pipeMat) })
            {
                Part(Shape.Cylinder, new Vector3(0f, y, back - 1.8f), new Vector3(0f, 0f, 90f), new Vector3(r * 2f, 128f, r * 2f), mat);
                for (float x = -60f; x <= 60f; x += 10f)
                    Part(Shape.Cylinder, new Vector3(x, y, back - 1.8f), new Vector3(0f, 0f, 90f), new Vector3(r * 2.5f, 0.5f, r * 2.5f), darkMat);
            }
            foreach (float x in new[] { -40f, 40f })
            {
                Part(Shape.Cylinder, new Vector3(x, 25f, back - 3.5f), Vector3.zero, new Vector3(3.6f, 50f, 3.6f), pipeMat);
                for (float y = 3f; y < 50f; y += 7f)
                    Part(Shape.Cylinder, new Vector3(x, y, back - 3.5f), Vector3.zero, new Vector3(4.3f, 0.6f, 4.3f), darkMat);
                Part(Shape.Box, new Vector3(x, 8f, back - 5.4f), Vector3.zero, new Vector3(1.4f, 1.8f, 0.4f), hazard);
            }

            // ---- Giant ventilation fan in the right wall ----
            Vector3 fanC = new Vector3(63f, 31f, 38f);
            Part(Shape.Cylinder, fanC, new Vector3(0f, 0f, 90f), new Vector3(20f, 1.2f, 20f), darkMat);
            Part(Shape.Cylinder, fanC + Vector3.left * 0.4f, new Vector3(0f, 0f, 90f), new Vector3(18.6f, 1.2f, 18.6f), plateMat);
            Part(Shape.Cylinder, fanC + Vector3.left * 0.9f, new Vector3(0f, 0f, 90f), new Vector3(3f, 1.2f, 3f), steelMat);
            for (int i = 0; i < 10; i++)
                Part(Shape.Box, fanC + Vector3.left * 0.8f, new Vector3(i * 18f, 0f, 0f), new Vector3(0.3f, 17.6f, 1.4f), darkMat);
            Part(Shape.Box, new Vector3(63.5f, 31f, 38f), Vector3.zero, new Vector3(0.5f, 22f, 0.8f), steelMat);

            // ---- Floor: cable runs, tool carts and cable reels ----
            Hose(new[] { new Vector3(-17f, 0.25f, -2f), new Vector3(-12f, 0.25f, -14f), new Vector3(-3f, 0.25f, -20f), new Vector3(10f, 0.25f, -21f), new Vector3(30f, 0.25f, -30f) }, 0.28f, darkMat);
            Hose(new[] { new Vector3(18f, 0.2f, 4f), new Vector3(26f, 0.2f, -6f), new Vector3(33f, 0.2f, -4f), new Vector3(48f, 0.2f, 6f) }, 0.2f, greenPipe);
            foreach (var p in new[] { new Vector3(-30f, 0f, -12f), new Vector3(32f, 0f, 22f) })
            {
                Part(Shape.Box, p + Vector3.up * 1.4f, new Vector3(0f, 20f, 0f), new Vector3(3f, 0.25f, 1.8f), steelMat);
                Part(Shape.Box, p + Vector3.up * 0.4f, new Vector3(0f, 20f, 0f), new Vector3(3f, 0.25f, 1.8f), steelMat);
                Part(Shape.Box, p + new Vector3(-0.4f, 1.9f, 0f), new Vector3(0f, 20f, 0f), new Vector3(1.2f, 0.8f, 0.9f), darkMat);
                Part(Shape.Box, p + new Vector3(0.9f, 1.75f, 0.2f), new Vector3(0f, 20f, 0f), new Vector3(0.9f, 0.5f, 0.6f), hazard);
            }
            foreach (var p in new[] { new Vector3(-38f, 0f, 2f), new Vector3(40f, 0f, -16f) })
            {
                Part(Shape.Cylinder, p + Vector3.up * 1.6f, new Vector3(0f, 0f, 90f), new Vector3(3.2f, 2.2f, 3.2f), darkMat);
                Part(Shape.Cylinder, p + Vector3.up * 1.6f, new Vector3(0f, 0f, 90f), new Vector3(2.6f, 2.3f, 2.6f), greenPipe);
            }

            BuildLightShafts();
        }

        /// <summary>A square beam between two points (local space).</summary>
        void Beam(Vector3 from, Vector3 to, float thickness, Material mat)
        {
            Vector3 d = to - from;
            var go = new GameObject("Beam");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.LookRotation(d.normalized);
            go.transform.localScale = new Vector3(thickness, thickness, d.magnitude);
            go.AddComponent<MeshFilter>().sharedMesh = Shapes.Get(Shape.Box);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        void Hose(Vector3[] pts, float radius, Material mat)
        {
            var go = new GameObject("Cable");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Shapes.Tube(pts, radius);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>A hanging chain of interlocking links ending in a heavy hook.</summary>
        void Chain(Vector3 top, float length, Material link, Material block)
        {
            const float step = 0.62f;
            int n = Mathf.Max(2, Mathf.RoundToInt(length / step));
            for (int i = 0; i < n; i++)
            {
                Vector3 p = top + Vector3.down * (i * step);
                float yaw = (i % 2) * 90f;
                // A link: two side bars and two end bars (an open rectangle).
                Quaternion r = Quaternion.Euler(0f, yaw, 0f);
                Part(Shape.Box, p + r * new Vector3(0.17f, 0f, 0f), new Vector3(0f, yaw, 0f), new Vector3(0.09f, 0.78f, 0.09f), link);
                Part(Shape.Box, p + r * new Vector3(-0.17f, 0f, 0f), new Vector3(0f, yaw, 0f), new Vector3(0.09f, 0.78f, 0.09f), link);
            }
            Vector3 end = top + Vector3.down * (n * step);
            // Hook block with sheave and the hook itself
            Part(Shape.Box, end + Vector3.down * 0.6f, Vector3.zero, new Vector3(1.4f, 1.2f, 0.7f), block);
            Part(Shape.Cylinder, end + Vector3.down * 0.5f, new Vector3(90f, 0f, 0f), new Vector3(1.0f, 0.8f, 1.0f), link);
            Part(Shape.Box, end + Vector3.down * 1.6f, Vector3.zero, new Vector3(0.3f, 1.0f, 0.3f), link);
            Part(Shape.Box, end + new Vector3(0.35f, -2.2f, 0f), new Vector3(0f, 0f, 35f), new Vector3(0.28f, 0.9f, 0.28f), link);
            Part(Shape.Box, end + new Vector3(0.62f, -1.85f, 0f), Vector3.zero, new Vector3(0.24f, 0.6f, 0.24f), link);
        }

        /// <summary>A welding torch throwing a constant spray of sparks, with a flickering light.</summary>
        void Welder(Vector3 worldPos, float side)
        {
            var a = GameAssets.Get();
            var go = new GameObject("Welder");
            go.transform.SetParent(transform, true);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.LookRotation(new Vector3(-side, -0.6f, -0.3f));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.6f, 0.2f));
            main.gravityModifier = 1.4f;
            main.maxParticles = 160;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            var em = ps.emission;
            em.rateOverTime = 70f;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 35f;
            sh.radius = 0.05f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.5f), new GradientColorKey(new Color(0.6f, 0.1f, 0.02f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = a.ParticleAdditive();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 2f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();

            // The arc: a tiny white-hot glow and a flickering light on the robot.
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(go.transform, false);
            core.transform.localScale = Vector3.one * 0.35f;
            core.GetComponent<Renderer>().sharedMaterial = a.Glow(new Color(0.8f, 0.9f, 1f), 4f);
            var lightGo = new GameObject("Arc light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = Vector3.forward * 0.6f;
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 14f;
            l.color = new Color(0.75f, 0.85f, 1f);
            l.shadows = LightShadows.None;
            lightGo.AddComponent<ArcFlicker>();
        }

        /// <summary>Soft shafts of cold light falling from the roof lamps through the haze.</summary>
        void BuildLightShafts()
        {
            var a = GameAssets.Get();
            var mat = new Material(a.ParticleAdditive()) { name = "LightShaft" };
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(0.32f, 0.42f, 0.55f, 0.11f));
            var mesh = ShaftMesh();
            foreach (var (pos, tilt, w) in new[]
            {
                (new Vector3(-20f, 24f, 32f), new Vector3(0f, 0f, 14f), 9f),
                (new Vector3(20f, 24f, 32f), new Vector3(0f, 0f, -12f), 9f),
                (new Vector3(-20f, 24f, 2f), new Vector3(10f, 0f, 10f), 8f),
                (new Vector3(22f, 24f, -12f), new Vector3(8f, 0f, -14f), 7f),
                (new Vector3(0f, 26f, 47f), new Vector3(-6f, 0f, 0f), 12f),
            })
            {
                for (int k = 0; k < 2; k++)
                {
                    var go = new GameObject("Shaft");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = pos;
                    go.transform.localRotation = Quaternion.Euler(tilt) * Quaternion.Euler(0f, k * 90f + 20f, 0f);
                    go.transform.localScale = new Vector3(w, 48f, 1f);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
        }

        static Mesh shaftMesh;

        /// <summary>A double-sided unit quad with white vertex colours (particle shaders multiply by them).</summary>
        static Mesh ShaftMesh()
        {
            if (shaftMesh != null) return shaftMesh;
            shaftMesh = new Mesh { name = "Shaft" };
            shaftMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            shaftMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            shaftMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            shaftMesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
            shaftMesh.RecalculateNormals();
            shaftMesh.RecalculateBounds();
            return shaftMesh;
        }
    }

    /// <summary>Makes a welding arc's light stutter like a real arc.</summary>
    public class ArcFlicker : MonoBehaviour
    {
        Light l;
        float next;

        void Awake() { l = GetComponent<Light>(); }

        void Update()
        {
            if (l == null || Time.unscaledTime < next) return;
            next = Time.unscaledTime + Random.Range(0.03f, 0.09f);
            l.intensity = Random.value < 0.15f ? 0.2f : Random.Range(2.5f, 5f);
        }
    }
}
