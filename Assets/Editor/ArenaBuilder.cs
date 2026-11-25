using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RobotArena.EditorTools
{
    /// <summary>
    /// Builds the battle arena scene from code (menu Robot Arena > Build Arena Scene,
    /// also run automatically by the cloud builds). The robots themselves are
    /// built at runtime by RobotFactory when a match starts.
    /// </summary>
    public static class ArenaBuilder
    {
        const string SceneDir = "Assets/Scenes";
        const string MaterialDir = "Assets/Materials";
        const string TextureDir = "Assets/Textures";
        public const string ScenePath = SceneDir + "/Arena.unity";

        const float ArenaSize = 280f;

        [MenuItem("Robot Arena/Build Arena Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        public static void BuildScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EnsureFolder(SceneDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(TextureDir);

            // ---- Material templates used at runtime ----
            var lit = Mat("LitTemplate", "Standard", Color.white, 0.5f, 0.5f);
            var emissive = Mat("EmissiveTemplate", "Standard", Color.white, 0f, 0.6f);
            emissive.EnableKeyword("_EMISSION");
            emissive.SetColor("_EmissionColor", Color.white);
            emissive.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(emissive);
            var additive = ParticleMat("ParticleAdditive", "Legacy Shaders/Particles/Additive");
            var alpha = ParticleMat("ParticleAlpha", "Legacy Shaders/Particles/Alpha Blended");

            var assetsGo = new GameObject("GameAssets");
            var ga = assetsGo.AddComponent<GameAssets>();
            ga.litTemplate = lit;
            ga.emissiveTemplate = emissive;
            ga.particleAdditive = additive;
            ga.particleAlpha = alpha;

            // ---- Arena materials ----
            var floorTex = FloorTexture();
            var floor = Mat("Floor", "Standard", new Color(0.55f, 0.55f, 0.53f), 0.1f, 0.25f);
            floor.mainTexture = floorTex;
            floor.mainTextureScale = new Vector2(ArenaSize / 32f, ArenaSize / 32f);
            EditorUtility.SetDirty(floor);
            var concrete = Mat("Concrete", "Standard", new Color(0.42f, 0.42f, 0.44f), 0.05f, 0.2f);
            var building = Mat("Building", "Standard", new Color(0.28f, 0.3f, 0.33f), 0.4f, 0.35f);
            var hazard = Mat("Hazard", "Standard", new Color(0.95f, 0.72f, 0.08f), 0.1f, 0.4f);
            var glowStrip = Mat("GlowStrip", "Standard", new Color(1f, 0.5f, 0.1f), 0f, 0.5f);
            glowStrip.EnableKeyword("_EMISSION");
            glowStrip.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.1f) * 2f);
            EditorUtility.SetDirty(glowStrip);
            var containerMats = new[]
            {
                Mat("ContainerRed", "Standard", new Color(0.6f, 0.15f, 0.1f), 0.3f, 0.3f),
                Mat("ContainerBlue", "Standard", new Color(0.12f, 0.3f, 0.55f), 0.3f, 0.3f),
                Mat("ContainerGreen", "Standard", new Color(0.2f, 0.4f, 0.22f), 0.3f, 0.3f),
                Mat("ContainerGrey", "Standard", new Color(0.5f, 0.5f, 0.52f), 0.3f, 0.3f)
            };

            // ---- Arena ----
            var arena = new GameObject("Arena").transform;
            Static(Prim(PrimitiveType.Cube, "Floor", arena, new Vector3(0f, -0.5f, 0f), Vector3.zero, new Vector3(ArenaSize, 1f, ArenaSize), floor));

            float half = ArenaSize * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float yaw = i * 90f;
                Quaternion r = Quaternion.Euler(0f, yaw, 0f);
                Vector3 c = r * new Vector3(0f, 9f, half + 2f);
                Static(Prim(PrimitiveType.Cube, "Wall", arena, c, new Vector3(0f, yaw, 0f), new Vector3(ArenaSize + 8f, 18f, 4f), concrete));
                Static(Prim(PrimitiveType.Cube, "WallTrim", arena, r * new Vector3(0f, 18.3f, half + 2f), new Vector3(0f, yaw, 0f), new Vector3(ArenaSize + 8f, 0.6f, 4.4f), hazard));
                Static(Prim(PrimitiveType.Cube, "WallLights", arena, r * new Vector3(0f, 6f, half - 0.05f), new Vector3(0f, yaw, 0f), new Vector3(ArenaSize, 0.4f, 0.2f), glowStrip));
                // Buttresses
                for (int k = -3; k <= 3; k++)
                    Static(Prim(PrimitiveType.Cube, "Buttress", arena, r * new Vector3(k * 38f, 10f, half - 1f), new Vector3(0f, yaw, 0f), new Vector3(4f, 20f, 4f), building));
            }

            // Central structures for cover
            Static(Prim(PrimitiveType.Cube, "Tower", arena, new Vector3(0f, 14f, 0f), new Vector3(0f, 45f, 0f), new Vector3(16f, 28f, 16f), building));
            Static(Prim(PrimitiveType.Cube, "TowerBand", arena, new Vector3(0f, 20f, 0f), new Vector3(0f, 45f, 0f), new Vector3(16.4f, 0.8f, 16.4f), glowStrip));
            Static(Prim(PrimitiveType.Cube, "TowerBand2", arena, new Vector3(0f, 8f, 0f), new Vector3(0f, 45f, 0f), new Vector3(16.4f, 0.8f, 16.4f), hazard));

            var blocks = new (Vector3 pos, Vector3 size, float yaw)[]
            {
                (new Vector3(-60f, 6f, 30f), new Vector3(24f, 12f, 14f), 15f),
                (new Vector3(62f, 8f, -20f), new Vector3(14f, 16f, 30f), -10f),
                (new Vector3(-45f, 4f, -55f), new Vector3(30f, 8f, 6f), 0f),
                (new Vector3(50f, 5f, 60f), new Vector3(10f, 10f, 10f), 30f),
                (new Vector3(-95f, 10f, -10f), new Vector3(12f, 20f, 12f), 0f),
                (new Vector3(95f, 10f, 30f), new Vector3(12f, 20f, 12f), 0f),
                (new Vector3(20f, 3f, -45f), new Vector3(6f, 6f, 22f), 60f),
            };
            foreach (var b in blocks)
            {
                Static(Prim(PrimitiveType.Cube, "Block", arena, b.pos, new Vector3(0f, b.yaw, 0f), b.size, building));
                Static(Prim(PrimitiveType.Cube, "BlockTrim", arena, new Vector3(b.pos.x, b.size.y + 0.2f, b.pos.z), new Vector3(0f, b.yaw, 0f), new Vector3(b.size.x + 0.4f, 0.4f, b.size.z + 0.4f), hazard));
            }

            // Ramps for the Raptor to launch off
            Ramp(arena, new Vector3(-30f, 0f, -20f), 90f, hazard, concrete);
            Ramp(arena, new Vector3(35f, 0f, 25f), -90f, hazard, concrete);
            Ramp(arena, new Vector3(0f, 0f, 55f), 180f, hazard, concrete);

            // Pillars
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad + 0.3f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 105f;
                Static(Prim(PrimitiveType.Cylinder, "Pillar", arena, p + Vector3.up * 12f, Vector3.zero, new Vector3(5f, 12f, 5f), concrete));
                Static(Prim(PrimitiveType.Cylinder, "PillarRing", arena, p + Vector3.up * 16f, Vector3.zero, new Vector3(5.6f, 0.4f, 5.6f), glowStrip));
            }

            // Shipping containers (physics props to smash through)
            var props = new GameObject("Containers").transform;
            var stacks = new (Vector3 pos, float yaw, int count)[]
            {
                (new Vector3(-20f, 0f, 30f), 10f, 3),
                (new Vector3(30f, 0f, -60f), -25f, 4),
                (new Vector3(-70f, 0f, 70f), 40f, 3),
                (new Vector3(75f, 0f, 75f), 0f, 2),
                (new Vector3(-80f, 0f, -80f), 75f, 3),
            };
            int ci = 0;
            foreach (var s in stacks)
            {
                for (int i = 0; i < s.count; i++)
                {
                    int layer = i < 2 ? 0 : 1;
                    float offset = layer == 0 ? (i - 0.5f) * 2.9f : 0f;
                    Quaternion r = Quaternion.Euler(0f, s.yaw, 0f);
                    Vector3 pos = s.pos + r * new Vector3(offset, 1.35f + layer * 2.7f, 0f);
                    var c = Prim(PrimitiveType.Cube, "Container", props, pos, new Vector3(0f, s.yaw, 0f), new Vector3(2.6f, 2.6f, 8f), containerMats[ci++ % containerMats.Length]);
                    var rb = c.AddComponent<Rigidbody>();
                    rb.mass = 4000f;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                }
            }

            // ---- Lighting and atmosphere ----
            var sun = Object.FindAnyObjectByType<Light>();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(38f, -40f, 0f);
                sun.color = new Color(1f, 0.9f, 0.78f);
                sun.intensity = 1.25f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;
            }
            // Sky: Unity's procedural sky, late-afternoon tint.
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                string skyPath = MaterialDir + "/Sky.mat";
                var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
                if (sky == null)
                {
                    sky = new Material(skyShader);
                    AssetDatabase.CreateAsset(sky, skyPath);
                }
                sky.shader = skyShader;
                if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.05f);
                if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 1.25f);
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.55f, 0.6f, 0.75f));
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.32f, 0.3f, 0.29f));
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1.25f);
                EditorUtility.SetDirty(sky);
                RenderSettings.skybox = sky;
            }
            if (sun != null) RenderSettings.sun = sun;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.66f, 0.68f, 0.72f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 650f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.44f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.19f, 0.18f);

            // ---- Camera ----
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 40f, -160f);
                cam.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
                cam.farClipPlane = 1500f;
                cam.nearClipPlane = 0.5f;
                cam.fieldOfView = 60f;
                cam.gameObject.AddComponent<FollowCamera>();
                cam.gameObject.AddComponent<MenuCameraOrbit>();
                cam.clearFlags = CameraClearFlags.Skybox;

                // Screen effects (bloom, grading). The material asset keeps the shader in the build.
                var postShader = Shader.Find("Hidden/RobotArena/Post");
                if (postShader != null)
                {
                    string postPath = MaterialDir + "/Post.mat";
                    var postMat = AssetDatabase.LoadAssetAtPath<Material>(postPath);
                    if (postMat == null)
                    {
                        postMat = new Material(postShader);
                        AssetDatabase.CreateAsset(postMat, postPath);
                    }
                    postMat.shader = postShader;
                    EditorUtility.SetDirty(postMat);
                    cam.gameObject.AddComponent<PostFx>().material = postMat;
                }
                else
                {
                    Debug.LogWarning("[ArenaBuilder] Post shader not found; screen effects disabled.");
                }
            }

            // ---- Match ----
            var matchGo = new GameObject("Match");
            var match = matchGo.AddComponent<MatchManager>();
            match.playerSpawn = Spawn("PlayerSpawn", new Vector3(0f, 0f, -100f), 0f);
            match.enemySpawns = new[]
            {
                Spawn("EnemySpawn1", new Vector3(-45f, 0f, 95f), 180f),
                Spawn("EnemySpawn2", new Vector3(45f, 0f, 95f), 180f)
            };

            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Robot Arena: arena scene built at " + ScenePath + ". Press Play.");
        }

        static void Ramp(Transform parent, Vector3 pos, float yaw, Material top, Material side)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            Static(Prim(PrimitiveType.Cube, "Ramp", parent, pos + r * new Vector3(0f, 1.6f, 0f), new Vector3(-12f, yaw, 0f), new Vector3(12f, 1f, 18f), top));
            Static(Prim(PrimitiveType.Cube, "RampBase", parent, pos + r * new Vector3(0f, 1.6f, -10.5f), new Vector3(0f, yaw, 0f), new Vector3(12f, 3.6f, 4f), side));
        }

        static Transform Spawn(string name, Vector3 pos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return go.transform;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Static(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static Material Mat(string name, string shaderName, Color color, float metallic, float smoothness)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.color = color;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material ParticleMat(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            string path = MaterialDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D FloorTexture()
        {
            string path = TextureDir + "/ArenaFloor.png";
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var rng = new System.Random(3);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = 0.82f + (float)rng.NextDouble() * 0.18f;
                bool major = x % 128 < 3 || y % 128 < 3;
                bool minor = x % 32 == 0 || y % 32 == 0;
                float v = n * (major ? 0.55f : 1f) * (minor ? 0.85f : 1f);
                tex.SetPixel(x, y, new Color(v, v, v, 1f));
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
