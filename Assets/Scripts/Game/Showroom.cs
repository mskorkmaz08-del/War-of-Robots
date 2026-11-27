using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// The hangar: a lit turntable outside the arena where the chosen robot is shown
    /// slowly rotating before the player deploys it. The robot is a real build with
    /// its brains switched off.
    /// </summary>
    public partial class Showroom : MonoBehaviour
    {
        public static bool Active { get; private set; }
        public static readonly Vector3 Spot = new Vector3(0f, 0f, -1200f);

        GameObject preview;
        RobotInfo info;
        float spin = 200f;
        // Menus and the online lobby show the robot centred behind the buttons;
        // the hangar frames it on the left beside the info card.
        bool centred;
        float dragVelocity;
        Camera cam;
        Light keyLight;

        public static Showroom Create()
        {
            var go = new GameObject("Showroom");
            var s = go.AddComponent<Showroom>();
            s.BuildStage();
            go.SetActive(false);
            return s;
        }

        void BuildStage()
        {
            var a = GameAssets.Get();
            transform.position = Spot;
            var floorMat = a.Paint(new Color(0.16f, 0.17f, 0.19f), 0.4f, 0.5f, true);
            var plateMat = a.Paint(new Color(0.22f, 0.23f, 0.25f), 0.6f, 0.55f, true);
            var darkMat = a.Paint(new Color(0.08f, 0.085f, 0.095f), 0.5f, 0.45f, false);
            var lamp = a.Glow(new Color(1f, 0.93f, 0.82f), 1.6f);
            var amber = a.Glow(UiKit.Orange, 1.4f);

            var steelMat = a.Paint(new Color(0.3f, 0.32f, 0.35f), 0.7f, 0.5f, true);
            var hazard = a.Paint(new Color(0.95f, 0.72f, 0.1f), 0.3f, 0.45f, false);
            var crateMat = a.Paint(new Color(0.27f, 0.32f, 0.22f), 0.2f, 0.35f, true);
            var redMat = a.Paint(new Color(0.55f, 0.12f, 0.08f), 0.4f, 0.5f, false);
            var coldLamp = a.Glow(new Color(0.75f, 0.88f, 1f), 1.8f);

            // ---- Floor: steel deck plates with painted bay markings ----
            Part(Shape.Box, new Vector3(0f, -0.5f, -5f), Vector3.zero, new Vector3(130f, 1f, 160f), floorMat);
            for (int i = -6; i <= 6; i++)
                Part(Shape.Box, new Vector3(i * 10f, 0.01f, -5f), Vector3.zero, new Vector3(0.15f, 0.02f, 160f), darkMat);
            for (int i = -8; i <= 7; i++)
                Part(Shape.Box, new Vector3(0f, 0.01f, i * 10f), Vector3.zero, new Vector3(130f, 0.02f, 0.15f), darkMat);
            // Yellow bay outline and hazard chevrons around the turntable
            foreach (float x in new[] { -24f, 24f })
                Part(Shape.Box, new Vector3(x, 0.03f, 0f), Vector3.zero, new Vector3(0.8f, 0.04f, 48f), hazard);
            foreach (float z in new[] { -24f, 24f })
                Part(Shape.Box, new Vector3(0f, 0.03f, z), Vector3.zero, new Vector3(48.8f, 0.04f, 0.8f), hazard);
            for (int i = -5; i <= 5; i++)
                Part(Shape.Box, new Vector3(i * 4f, 0.04f, -26f), new Vector3(0f, 45f, 0f), new Vector3(0.9f, 0.04f, 3f), hazard);

            // Turntable: heavy steel plate with a thin amber edge light.
            Part(Shape.Cylinder, new Vector3(0f, 0.3f, 0f), Vector3.zero, new Vector3(34f, 0.6f, 34f), plateMat);
            Part(Shape.Cylinder, new Vector3(0f, 0.62f, 0f), Vector3.zero, new Vector3(31f, 0.1f, 31f), darkMat);
            Part(Shape.Cylinder, new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(34.4f, 0.12f, 34.4f), amber);
            for (int i = 0; i < 12; i++)
            {
                float ang = i * 30f;
                Vector3 o = Quaternion.Euler(0f, ang, 0f) * new Vector3(0f, 0.66f, 14f);
                Part(Shape.Box, o, new Vector3(0f, ang, 0f), new Vector3(0.5f, 0.06f, 2.2f), hazard);
            }

            // ---- Back wall with a giant closed bay door ----
            const float W = 65f, H = 50f, back = 70f, front = -85f;
            Part(Shape.Box, new Vector3(0f, H * 0.5f, back), Vector3.zero, new Vector3(W * 2f, H, 2f), plateMat);
            Part(Shape.Box, new Vector3(0f, 17f, back - 1.2f), Vector3.zero, new Vector3(50f, 34f, 0.6f), steelMat);
            for (int i = 0; i < 16; i++)
                Part(Shape.Box, new Vector3(0f, 1.5f + i * 2.1f, back - 1.6f), Vector3.zero, new Vector3(49f, 0.35f, 0.4f), darkMat);
            for (int i = -6; i <= 6; i++)
                Part(Shape.Box, new Vector3(i * 4f, 1.2f, back - 1.6f), new Vector3(0f, 0f, 45f), new Vector3(1.2f, 1.2f, 0.3f), (i & 1) == 0 ? hazard : darkMat);
            foreach (float x in new[] { -26.5f, 26.5f })
            {
                Part(Shape.Box, new Vector3(x, 18f, back - 1.5f), Vector3.zero, new Vector3(3f, 36f, 3f), darkMat);
                Part(Shape.Box, new Vector3(x, 30f, back - 3.1f), Vector3.zero, new Vector3(1.2f, 1.2f, 0.4f), redMat);
                Part(Shape.Box, new Vector3(x, 30f, back - 3.35f), Vector3.zero, new Vector3(0.7f, 0.7f, 0.1f), amber);
            }
            Part(Shape.Box, new Vector3(0f, 35.5f, back - 1.5f), Vector3.zero, new Vector3(56f, 3f, 3f), darkMat);
            Part(Shape.Box, new Vector3(0f, 35.5f, back - 3.05f), Vector3.zero, new Vector3(30f, 1.0f, 0.1f), coldLamp);
            // Bay number
            Part(Shape.Box, new Vector3(-36f, 30f, back - 1.1f), Vector3.zero, new Vector3(8f, 10f, 0.2f), hazard);
            Part(Shape.Box, new Vector3(-36f, 30f, back - 1.25f), Vector3.zero, new Vector3(1.4f, 7f, 0.1f), darkMat);

            // ---- Side walls: ribbed with I-beam columns, vents and a catwalk ----
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * W;
                Part(Shape.Box, new Vector3(x, H * 0.5f, (back + front) * 0.5f), Vector3.zero, new Vector3(2f, H, back - front), plateMat);
                for (float z = front + 10f; z < back; z += 15f)
                {
                    Part(Shape.Box, new Vector3(x - sx * 1.5f, H * 0.5f, z), Vector3.zero, new Vector3(1f, H, 2.4f), darkMat);
                    Part(Shape.Box, new Vector3(x - sx * 2.2f, H * 0.5f, z), Vector3.zero, new Vector3(0.6f, H, 1.2f), steelMat);
                    Part(Shape.Box, new Vector3(x - sx * 1.2f, 8f, z + 7.5f), Vector3.zero, new Vector3(0.4f, 4f, 6f), darkMat);
                    for (int k = 0; k < 5; k++)
                        Part(Shape.Box, new Vector3(x - sx * 1.45f, 6.6f + k * 0.7f, z + 7.5f), Vector3.zero, new Vector3(0.2f, 0.18f, 5.6f), steelMat);
                    Part(Shape.Box, new Vector3(x - sx * 1.3f, 26f, z + 7.5f), Vector3.zero, new Vector3(0.3f, 0.5f, 8f), coldLamp);
                }
                // Catwalk with railing
                Part(Shape.Box, new Vector3(x - sx * 4.5f, 16f, -5f), Vector3.zero, new Vector3(6f, 0.4f, back - front - 4f), steelMat);
                Part(Shape.Box, new Vector3(x - sx * 7.4f, 17.2f, -5f), Vector3.zero, new Vector3(0.15f, 0.15f, back - front - 4f), hazard);
                Part(Shape.Box, new Vector3(x - sx * 7.4f, 16.6f, -5f), Vector3.zero, new Vector3(0.1f, 0.1f, back - front - 4f), darkMat);
                for (float z = front + 4f; z < back - 2f; z += 4f)
                {
                    Part(Shape.Box, new Vector3(x - sx * 7.4f, 16.8f, z), Vector3.zero, new Vector3(0.12f, 1.2f, 0.12f), darkMat);
                    Part(Shape.Box, new Vector3(x - sx * 4.5f, 15.2f, z), new Vector3(0f, 0f, sx * 35f), new Vector3(0.2f, 2.2f, 0.2f), darkMat);
                }
            }
            // Front wall (behind the camera) keeps the bay enclosed
            Part(Shape.Box, new Vector3(0f, H * 0.5f, front), Vector3.zero, new Vector3(W * 2f, H, 2f), plateMat);

            // ---- Roof: trusses and light strips ----
            Part(Shape.Box, new Vector3(0f, H, (back + front) * 0.5f), Vector3.zero, new Vector3(W * 2f, 1f, back - front), darkMat);
            for (float z = front + 10f; z < back; z += 15f)
            {
                Part(Shape.Box, new Vector3(0f, H - 2f, z), Vector3.zero, new Vector3(W * 2f, 1.2f, 1f), steelMat);
                Part(Shape.Box, new Vector3(0f, H - 5f, z), Vector3.zero, new Vector3(W * 2f, 0.6f, 0.6f), steelMat);
                for (int i = -6; i <= 6; i++)
                    Part(Shape.Box, new Vector3(i * 10f + 2.5f, H - 3.5f, z), new Vector3(0f, 0f, 50f), new Vector3(0.35f, 4f, 0.35f), steelMat);
                Part(Shape.Box, new Vector3(-20f, H - 5.6f, z + 7.5f), Vector3.zero, new Vector3(1.2f, 0.4f, 9f), lamp);
                Part(Shape.Box, new Vector3(20f, H - 5.6f, z + 7.5f), Vector3.zero, new Vector3(1.2f, 0.4f, 9f), lamp);
            }

            // ---- Gantry crane over the bay ----
            foreach (float x in new[] { -58f, 58f })
                Part(Shape.Box, new Vector3(x, 38f, -5f), Vector3.zero, new Vector3(2f, 2f, back - front - 6f), darkMat);
            Part(Shape.Box, new Vector3(0f, 36.5f, 20f), Vector3.zero, new Vector3(118f, 2.6f, 2.2f), hazard);
            Part(Shape.Box, new Vector3(0f, 36.5f, 23f), Vector3.zero, new Vector3(118f, 2.6f, 2.2f), hazard);
            Part(Shape.Box, new Vector3(-18f, 34.6f, 21.5f), Vector3.zero, new Vector3(5f, 2.4f, 6f), darkMat);
            Part(Shape.Cylinder, new Vector3(-18f, 27f, 21.5f), Vector3.zero, new Vector3(0.25f, 13f, 0.25f), steelMat);
            Part(Shape.Box, new Vector3(-18f, 20f, 21.5f), Vector3.zero, new Vector3(2.4f, 1.6f, 1.4f), hazard);

            // ---- Maintenance arm beside the turntable ----
            Part(Shape.Box, new Vector3(30f, 1.5f, 12f), Vector3.zero, new Vector3(5f, 3f, 5f), darkMat);
            Part(Shape.Box, new Vector3(30f, 9f, 12f), new Vector3(0f, 0f, -12f), new Vector3(1.8f, 13f, 1.8f), hazard);
            Part(Shape.Cylinder, new Vector3(31.4f, 15.5f, 12f), new Vector3(0f, 0f, 90f), new Vector3(2.4f, 2.4f, 2.4f), darkMat);
            Part(Shape.Box, new Vector3(26.5f, 17.5f, 12f), new Vector3(0f, 0f, 62f), new Vector3(1.4f, 10f, 1.4f), hazard);
            Part(Shape.Box, new Vector3(22f, 19.8f, 12f), Vector3.zero, new Vector3(1.4f, 1.0f, 1.0f), darkMat);
            Part(Shape.Box, new Vector3(21.2f, 19.8f, 12f), Vector3.zero, new Vector3(0.4f, 0.3f, 0.3f), coldLamp);

            // ---- Crates, fuel drums and a weapon rack along the walls ----
            void Crate(Vector3 p, float size, float yaw)
            {
                Part(Shape.Box, p + Vector3.up * size * 0.5f, new Vector3(0f, yaw, 0f), Vector3.one * size, crateMat);
                Part(Shape.Box, p + Vector3.up * size * 0.5f, new Vector3(0f, yaw, 0f), new Vector3(size * 1.02f, size * 0.12f, size * 1.02f), darkMat);
            }
            Crate(new Vector3(-50f, 0f, 40f), 6f, 5f);
            Crate(new Vector3(-50f, 6f, 40f), 4.5f, -8f);
            Crate(new Vector3(-43f, 0f, 44f), 5f, 20f);
            Crate(new Vector3(48f, 0f, 46f), 6f, -4f);
            Crate(new Vector3(52f, 0f, 36f), 4f, 12f);
            Crate(new Vector3(-52f, 0f, -20f), 5f, 0f);
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(40f + (i % 3) * 2.6f, 0f, 28f + (i / 3) * 2.6f);
                Part(Shape.Cylinder, p + Vector3.up * 1.6f, Vector3.zero, new Vector3(2.2f, 3.2f, 2.2f), i % 2 == 0 ? redMat : steelMat);
                Part(Shape.Cylinder, p + Vector3.up * 2.2f, Vector3.zero, new Vector3(2.3f, 0.2f, 2.3f), darkMat);
            }
            // Weapon rack: spare gun barrels on a frame
            Part(Shape.Box, new Vector3(-46f, 6f, 10f), Vector3.zero, new Vector3(1f, 12f, 16f), darkMat);
            for (int i = 0; i < 4; i++)
            {
                Part(Shape.Box, new Vector3(-45f, 3f + i * 2.6f, 10f), Vector3.zero, new Vector3(1.4f, 0.4f, 15f), steelMat);
                Part(Shape.Cylinder, new Vector3(-44f, 4f + i * 2.6f, 10f), new Vector3(90f, 0f, 0f), new Vector3(1.2f, 12f, 1.2f), i % 2 == 0 ? darkMat : steelMat);
            }

            BuildDetails(darkMat, steelMat, plateMat, hazard, coldLamp);

            MeshMerge.Merge(transform);
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Performance.AddReflections(transform, Spot + Vector3.up * 20f, new Vector3(130f, 50f, 155f));

            var lightGo = new GameObject("Key light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(-25f, 30f, -25f);
            keyLight = lightGo.AddComponent<Light>();
            keyLight.type = LightType.Point;
            keyLight.range = 90f;
            keyLight.intensity = 1.6f;
            keyLight.color = new Color(1f, 0.95f, 0.88f);
            keyLight.shadows = LightShadows.None;

            var top = new GameObject("Bay light");
            top.transform.SetParent(transform, false);
            top.transform.localPosition = new Vector3(0f, 40f, 5f);
            var tl = top.AddComponent<Light>();
            tl.type = LightType.Point;
            tl.range = 120f;
            tl.intensity = 1.2f;
            tl.color = new Color(0.85f, 0.92f, 1f);
            tl.shadows = LightShadows.None;

            var rim = new GameObject("Rim light");
            rim.transform.SetParent(transform, false);
            rim.transform.localPosition = new Vector3(22f, 18f, 30f);
            var rl = rim.AddComponent<Light>();
            rl.type = LightType.Point;
            rl.range = 70f;
            rl.intensity = 2f;
            rl.color = new Color(0.45f, 0.75f, 1f);
            rl.shadows = LightShadows.None;
        }

        void Part(Shape shape, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            var go = new GameObject(shape.ToString());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Shapes.Get(shape);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        public void Open(RobotInfo robot, bool centred = false)
        {
            this.centred = centred;
            if (Active && info == robot && preview != null) return;
            Active = true;
            gameObject.SetActive(true);
            cam = Camera.main;
            Show(robot);
        }

        public void Close()
        {
            Active = false;
            if (preview != null) Destroy(preview);
            preview = null;
            gameObject.SetActive(false);
        }

        public void Show(RobotInfo robot)
        {
            info = robot;
            if (preview != null) Destroy(preview);
            preview = RobotFactory.Build(robot.type, Spot + Vector3.up * 0.7f, Quaternion.Euler(0f, spin, 0f), 0, Livery.Player, true, robot.name, Loadout.ForPlayer(robot.type), 1f);
            Freeze(preview);
            Fx.Dust(Spot + Vector3.up * 0.8f, 3f);
            GameAudio.Play2D(Sfx.Landing, 0.4f);
        }

        /// <summary>Turns off physics, weapons and AI so the robot just stands there.</summary>
        static void Freeze(GameObject go)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
            foreach (var b in go.GetComponents<MonoBehaviour>()) b.enabled = false;
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        /// <summary>Lets the player spin the robot by dragging (degrees).</summary>
        public void Drag(float degrees)
        {
            dragVelocity = degrees / Mathf.Max(0.001f, Time.unscaledDeltaTime);
            spin -= degrees;
        }

        void LateUpdate()
        {
            if (preview == null || info == null) return;
            float dt = Time.unscaledDeltaTime;
            dragVelocity = Mathf.Lerp(dragVelocity, 0f, 1f - Mathf.Exp(-4f * dt));
            spin += dt * 14f;
            preview.transform.rotation = Quaternion.Euler(0f, spin, 0f);
            var walker = preview.GetComponent<WalkerLocomotion>();
            if (walker != null) walker.PoseLegs();

            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            // Frame the robot on the left of the screen; the info panel sits on the right.
            float d = info.previewDistance;
            Vector3 focus = Spot + Vector3.up * info.previewHeight;
            if (centred) d *= 1.25f;
            Vector3 side = centred ? Vector3.zero : Vector3.right * d * 0.3f;
            Vector3 camPos = focus + side + new Vector3(0f, d * 0.12f, -d);
            cam.transform.position = Vector3.Lerp(cam.transform.position, camPos, 1f - Mathf.Exp(-6f * dt));
            Quaternion look = Quaternion.LookRotation(focus + side - cam.transform.position);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, look, 1f - Mathf.Exp(-8f * dt));
        }
    }
}
