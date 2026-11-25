using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// The hangar: a lit turntable outside the arena where the chosen robot is shown
    /// slowly rotating before the player deploys it. The robot is a real build with
    /// its brains switched off.
    /// </summary>
    public class Showroom : MonoBehaviour
    {
        public static bool Active { get; private set; }
        public static readonly Vector3 Spot = new Vector3(0f, 0f, -1200f);

        GameObject preview;
        RobotInfo info;
        float spin = 200f;
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

            Part(Shape.Box, new Vector3(0f, -0.5f, 0f), Vector3.zero, new Vector3(400f, 1f, 400f), floorMat);
            // Turntable: heavy steel plate with a thin amber edge light.
            Part(Shape.Cylinder, new Vector3(0f, 0.3f, 0f), Vector3.zero, new Vector3(34f, 0.6f, 34f), plateMat);
            Part(Shape.Cylinder, new Vector3(0f, 0.62f, 0f), Vector3.zero, new Vector3(31f, 0.1f, 31f), darkMat);
            Part(Shape.Cylinder, new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(34.4f, 0.12f, 34.4f), darkMat);
            // Hangar back wall: steel panels with recessed warm light bars.
            Part(Shape.Box, new Vector3(0f, 20f, 60f), Vector3.zero, new Vector3(160f, 40f, 2f), plateMat);
            for (int i = -3; i <= 3; i++)
            {
                Part(Shape.Box, new Vector3(i * 20f, 20f, 58.6f), Vector3.zero, new Vector3(4f, 40f, 1.2f), darkMat);
                Part(Shape.Box, new Vector3(i * 20f, 20f, 58f), Vector3.zero, new Vector3(0.5f, 30f, 0.2f), lamp);
            }
            Part(Shape.Box, new Vector3(0f, 38.5f, 58.6f), Vector3.zero, new Vector3(160f, 3f, 1.5f), darkMat);
            // Side pylons.
            foreach (float x in new[] { -46f, 46f })
            {
                Part(Shape.Box, new Vector3(x, 14f, 30f), Vector3.zero, new Vector3(6f, 28f, 6f), plateMat);
                Part(Shape.Box, new Vector3(x, 26f, 26.9f), Vector3.zero, new Vector3(3f, 0.4f, 0.3f), amber);
            }
            MeshMerge.Merge(transform);

            Performance.AddReflections(transform, Spot + Vector3.up * 15f, new Vector3(200f, 80f, 200f));

            var lightGo = new GameObject("Key light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(-25f, 30f, -25f);
            keyLight = lightGo.AddComponent<Light>();
            keyLight.type = LightType.Point;
            keyLight.range = 90f;
            keyLight.intensity = 1.6f;
            keyLight.color = new Color(1f, 0.95f, 0.88f);
            keyLight.shadows = LightShadows.None;

            var rim = new GameObject("Rim light");
            rim.transform.SetParent(transform, false);
            rim.transform.localPosition = new Vector3(22f, 18f, 30f);
            var rl = rim.AddComponent<Light>();
            rl.type = LightType.Point;
            rl.range = 70f;
            rl.intensity = 2f;
            rl.color = UiKit.Orange;
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

        public void Open(RobotInfo robot)
        {
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
            preview = RobotFactory.Build(robot.type, Spot + Vector3.up * 0.7f, Quaternion.Euler(0f, spin, 0f), 0, Livery.Player, true, robot.name);
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
            Vector3 side = Vector3.right * d * 0.3f;
            Vector3 camPos = focus + side + new Vector3(0f, d * 0.12f, -d);
            cam.transform.position = Vector3.Lerp(cam.transform.position, camPos, 1f - Mathf.Exp(-6f * dt));
            Quaternion look = Quaternion.LookRotation(focus + side - cam.transform.position);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, look, 1f - Mathf.Exp(-8f * dt));
        }
    }
}
