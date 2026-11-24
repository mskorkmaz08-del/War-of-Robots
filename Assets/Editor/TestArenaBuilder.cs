using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RobotArena.EditorTools
{
    /// <summary>
    /// Builds the whole test scene from code so there is no manual setup:
    /// menu Robot Arena > Build Test Arena Scene.
    /// Creates an arena, a player robot, a dummy robot to push around,
    /// physics crates, a chase camera and touch controls.
    /// </summary>
    public static class TestArenaBuilder
    {
        const string SceneDir = "Assets/Scenes";
        const string MaterialDir = "Assets/Materials";
        public const string ScenePath = SceneDir + "/TestArena.unity";

        [MenuItem("Robot Arena/Build Test Arena Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        /// <summary>Builds and saves the scene without asking anything (used by cloud builds).</summary>
        public static void BuildScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EnsureFolder(SceneDir);
            EnsureFolder(MaterialDir);

            var floorMat = GetMaterial("Floor", new Color(0.22f, 0.24f, 0.27f), 0.1f, 0.3f);
            var wallMat = GetMaterial("Wall", new Color(0.35f, 0.37f, 0.4f), 0.2f, 0.4f);
            var hazardMat = GetMaterial("Hazard", new Color(0.95f, 0.75f, 0.1f), 0.1f, 0.4f);
            var crateMat = GetMaterial("Crate", new Color(0.55f, 0.38f, 0.2f), 0f, 0.2f);
            var playerMat = GetMaterial("PlayerBody", new Color(0.15f, 0.45f, 0.95f), 0.7f, 0.6f);
            var enemyMat = GetMaterial("EnemyBody", new Color(0.9f, 0.2f, 0.15f), 0.7f, 0.6f);
            var metalMat = GetMaterial("Metal", new Color(0.7f, 0.72f, 0.75f), 0.9f, 0.7f);
            var rubberMat = GetMaterial("Rubber", new Color(0.08f, 0.08f, 0.08f), 0f, 0.3f);

            BuildArena(floorMat, wallMat, hazardMat, crateMat);

            var player = BuildRobot("PlayerRobot", playerMat, metalMat, rubberMat,
                new Vector3(0f, 0.7f, -6f), Quaternion.identity, true);
            BuildRobot("DummyRobot", enemyMat, metalMat, rubberMat,
                new Vector3(0f, 0.7f, 4f), Quaternion.Euler(0f, 180f, 0f), false);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 4f, -13f);
                var follow = cam.gameObject.AddComponent<FollowCamera>();
                follow.target = player.transform;
            }

            BuildHud(player.GetComponent<RobotInput>());

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);

            Debug.Log("Robot Arena: test scene built at " + ScenePath + ". Press Play. Drive with WASD or arrows, flip with Space.");
        }

        // ---------- Arena ----------

        static void BuildArena(Material floorMat, Material wallMat, Material hazardMat, Material crateMat)
        {
            var arena = new GameObject("Arena").transform;
            const float size = 40f;

            Prim(PrimitiveType.Cube, "Floor", arena, new Vector3(0f, -0.5f, 0f), Vector3.zero,
                new Vector3(size, 1f, size), floorMat);

            float half = size * 0.5f;
            Prim(PrimitiveType.Cube, "Wall N", arena, new Vector3(0f, 1f, half), Vector3.zero, new Vector3(size, 2f, 1f), wallMat);
            Prim(PrimitiveType.Cube, "Wall S", arena, new Vector3(0f, 1f, -half), Vector3.zero, new Vector3(size, 2f, 1f), wallMat);
            Prim(PrimitiveType.Cube, "Wall E", arena, new Vector3(half, 1f, 0f), Vector3.zero, new Vector3(1f, 2f, size), wallMat);
            Prim(PrimitiveType.Cube, "Wall W", arena, new Vector3(-half, 1f, 0f), Vector3.zero, new Vector3(1f, 2f, size), wallMat);

            Prim(PrimitiveType.Cube, "Ramp", arena, new Vector3(9f, 0.45f, 0f), new Vector3(-14f, 90f, 0f),
                new Vector3(4f, 0.3f, 6f), hazardMat);
            Prim(PrimitiveType.Cube, "Ramp 2", arena, new Vector3(-9f, 0.45f, 8f), new Vector3(-14f, -90f, 0f),
                new Vector3(4f, 0.3f, 6f), hazardMat);

            var crates = new GameObject("Crates").transform;
            crates.SetParent(arena);
            for (int i = 0; i < 8; i++)
            {
                float x = -6f + (i % 4) * 1.1f;
                float y = 0.4f + (i / 4) * 0.82f;
                var crate = Prim(PrimitiveType.Cube, "Crate " + (i + 1), crates, new Vector3(x, y, 9f), Vector3.zero,
                    Vector3.one * 0.8f, crateMat);
                var rb = crate.AddComponent<Rigidbody>();
                rb.mass = 15f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        // ---------- Robot ----------

        static GameObject BuildRobot(string name, Material bodyMat, Material metalMat, Material rubberMat,
            Vector3 position, Quaternion rotation, bool playerControlled)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, rotation);

            // Body: one rigidbody, child colliders make a compound shape.
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var rb = body.AddComponent<Rigidbody>();
            rb.mass = 120f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            Prim(PrimitiveType.Cube, "Chassis", body.transform, new Vector3(0f, 0.1f, 0f), Vector3.zero,
                new Vector3(1.4f, 0.4f, 1.8f), bodyMat);
            Prim(PrimitiveType.Cube, "Armor Top", body.transform, new Vector3(0f, 0.35f, -0.15f), Vector3.zero,
                new Vector3(1.2f, 0.12f, 1.2f), metalMat);
            Prim(PrimitiveType.Cube, "Armor Rear", body.transform, new Vector3(0f, 0.15f, -0.95f), Vector3.zero,
                new Vector3(1.3f, 0.35f, 0.1f), metalMat);
            var eye = Prim(PrimitiveType.Sphere, "Sensor", body.transform, new Vector3(0f, 0.48f, 0.3f), Vector3.zero,
                Vector3.one * 0.22f, rubberMat);
            Object.DestroyImmediate(eye.GetComponent<Collider>());

            // Wheels
            var wheels = new List<RobotDrive.Wheel>();
            float[] xs = { -0.82f, 0.82f };
            float[] zs = { 0.6f, -0.6f };
            foreach (float x in xs)
            foreach (float z in zs)
            {
                bool left = x < 0f;
                string label = (left ? "L" : "R") + (z > 0 ? "F" : "B");

                var wheelGo = new GameObject("Wheel " + label);
                wheelGo.transform.SetParent(body.transform, false);
                wheelGo.transform.localPosition = new Vector3(x, 0f, z);
                var wc = wheelGo.AddComponent<WheelCollider>();
                wc.radius = 0.35f;
                wc.mass = 3f;
                wc.suspensionDistance = 0.15f;
                wc.forceAppPointDistance = 0.1f;
                var spring = wc.suspensionSpring;
                spring.spring = 9000f;
                spring.damper = 900f;
                spring.targetPosition = 0.5f;
                wc.suspensionSpring = spring;
                var fwd = wc.forwardFriction;
                fwd.stiffness = 1.4f;
                wc.forwardFriction = fwd;
                var side = wc.sidewaysFriction;
                side.stiffness = 0.8f;
                wc.sidewaysFriction = side;

                var visual = new GameObject("Wheel Visual " + label);
                visual.transform.SetParent(body.transform, false);
                visual.transform.localPosition = new Vector3(x, 0f, z);
                var tyre = Prim(PrimitiveType.Cylinder, "Tyre", visual.transform, Vector3.zero, new Vector3(0f, 0f, 90f),
                    new Vector3(0.7f, 0.08f, 0.7f), rubberMat);
                Object.DestroyImmediate(tyre.GetComponent<Collider>());
                var hub = Prim(PrimitiveType.Cylinder, "Hub", visual.transform, new Vector3(left ? -0.05f : 0.05f, 0f, 0f),
                    new Vector3(0f, 0f, 90f), new Vector3(0.3f, 0.06f, 0.3f), metalMat);
                Object.DestroyImmediate(hub.GetComponent<Collider>());

                wheels.Add(new RobotDrive.Wheel { collider = wc, visual = visual.transform, leftSide = left });
            }

            RobotInput input = null;
            if (playerControlled) input = body.AddComponent<RobotInput>();

            var drive = body.AddComponent<RobotDrive>();
            drive.input = input;
            drive.wheels = wheels.ToArray();

            // Flipper: separate rigidbody on a hinge at the front edge.
            var flipper = new GameObject("Flipper");
            flipper.transform.SetParent(root.transform, false);
            flipper.transform.localPosition = new Vector3(0f, -0.05f, 0.88f);
            Prim(PrimitiveType.Cube, "Plate", flipper.transform, new Vector3(0f, 0f, 0.35f), Vector3.zero,
                new Vector3(1.3f, 0.08f, 0.7f), metalMat);
            var flipperRb = flipper.AddComponent<Rigidbody>();
            flipperRb.mass = 8f;
            flipperRb.interpolation = RigidbodyInterpolation.Interpolate;
            flipperRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var hinge = flipper.AddComponent<HingeJoint>();
            hinge.connectedBody = rb;
            hinge.anchor = Vector3.zero;
            hinge.axis = Vector3.right;
            hinge.enableCollision = false;
            var flip = flipper.AddComponent<RobotFlipper>();
            flip.input = input;

            drive.attachedParts = new[] { flipperRb };

            return body;
        }

        // ---------- HUD / touch controls ----------

        static void BuildHud(RobotInput input)
        {
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // The new scene never has an EventSystem yet, so always add one.
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif

            var controls = new GameObject("MobileControls", typeof(RectTransform));
            controls.transform.SetParent(canvasGo.transform, false);
            Stretch(controls.GetComponent<RectTransform>());
            controls.AddComponent<MobileControls>();

            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // Joystick, bottom left
            var stickBg = UiImage("Joystick", controls.transform, knob, new Color(1f, 1f, 1f, 0.22f),
                new Vector2(0f, 0f), new Vector2(260f, 260f), new Vector2(300f, 300f));
            var stickHandle = UiImage("Handle", stickBg.transform, knob, new Color(1f, 1f, 1f, 0.6f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f));
            stickHandle.raycastTarget = false;
            var joystick = stickBg.gameObject.AddComponent<VirtualJoystick>();
            joystick.background = stickBg.rectTransform;
            joystick.handle = stickHandle.rectTransform;
            joystick.radius = 120f;

            // Attack button, bottom right
            var attack = UiImage("Flip Button", controls.transform, knob, new Color(1f, 0.3f, 0.2f, 0.55f),
                new Vector2(1f, 0f), new Vector2(-240f, 240f), new Vector2(230f, 230f));
            var button = attack.gameObject.AddComponent<TouchButton>();
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(attack.transform, false);
            Stretch(label.GetComponent<RectTransform>());
            var text = label.GetComponent<Text>();
            text.text = "FLIP";
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 48;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
            text.font = LoadBuiltinFont();

            if (input != null)
            {
                input.joystick = joystick;
                input.attackButton = button;
            }
        }

        static Font LoadBuiltinFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            }
            return font;
        }

        static Image UiImage(string name, Transform parent, Sprite sprite, Color color,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ---------- Helpers ----------

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos,
            Vector3 localEuler, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static Material GetMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == path))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
