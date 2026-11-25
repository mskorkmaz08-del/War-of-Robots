using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>A point of interest shown on the HUD: capture points, depots, the reactor, the commander.</summary>
    public class WorldMarker
    {
        public string label;
        public Color color = Color.white;
        public Vector3 position;
        /// <summary>When set, the marker follows this robot or building and hides when it is destroyed.</summary>
        public Damageable target;
        public float height = 8f;
        public bool visible = true;

        public bool Active => visible && (target == null || !target.IsDead);
        public Vector3 WorldPos => (target != null ? target.Center : position) + Vector3.up * height;
    }

    /// <summary>
    /// A capture point: a glowing ring on the ground. Robots of one team standing inside
    /// with no enemies present slowly take it over. Contested points don't move.
    /// </summary>
    public class CaptureZone : MonoBehaviour
    {
        public static readonly List<CaptureZone> All = new List<CaptureZone>();

        public string label = "A";
        public float radius = 20f;
        public float captureTime = 7f;

        /// <summary>-1 neutral, 0 your team, 1 enemy team.</summary>
        public int Owner { get; private set; } = -1;
        /// <summary>-1..1; positive leans to your team.</summary>
        public float Progress { get; private set; }
        public int Count0 { get; private set; }
        public int Count1 { get; private set; }
        public bool Contested => Count0 > 0 && Count1 > 0;

        public event System.Action<CaptureZone, int> Captured;

        /// <summary>Online clients: ownership comes from the host.</summary>
        public bool netDriven;

        public void NetSet(int owner, float progress)
        {
            Owner = owner;
            Progress = progress;
            Refresh();
        }

        MeshRenderer ring;
        MeshRenderer beacon;
        Material neutralMat, allyMat, enemyMat;
        int shown = -2;

        public static Color TeamColor(int owner) => owner == 0 ? UiKit.Cyan : owner == 1 ? UiKit.Red : new Color(0.9f, 0.9f, 0.85f);

        public static CaptureZone Create(Vector3 pos, float radius, string label)
        {
            var go = new GameObject("Zone " + label);
            go.transform.position = new Vector3(pos.x, 0f, pos.z);
            var z = go.AddComponent<CaptureZone>();
            z.radius = radius;
            z.label = label;
            z.BuildVisual();
            return z;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void BuildVisual()
        {
            var a = GameAssets.Get();
            if (a == null) return;
            neutralMat = a.Glow(new Color(0.9f, 0.9f, 0.85f), 1.2f);
            allyMat = a.Glow(UiKit.Cyan, 1.8f);
            enemyMat = a.Glow(UiKit.Red, 1.8f);

            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            ringGo.AddComponent<MeshFilter>().sharedMesh = RingMesh(radius - 0.9f, radius, 72);
            ring = ringGo.AddComponent<MeshRenderer>();
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Beacon post at the centre (on top of whatever is there, e.g. the Foundry tower).
            float baseY = 0f;
            if (Physics.Raycast(transform.position + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 90f, ~0, QueryTriggerInteraction.Ignore)
                && hit.rigidbody == null)
                baseY = hit.point.y;
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(post.GetComponent<Collider>());
            post.name = "Beacon";
            post.transform.SetParent(transform, false);
            post.transform.localPosition = new Vector3(0f, baseY + 7f, 0f);
            post.transform.localScale = new Vector3(0.7f, 7f, 0.7f);
            beacon = post.GetComponent<MeshRenderer>();
            beacon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(transform, false);
            cap.transform.localPosition = new Vector3(0f, baseY + 0.4f, 0f);
            cap.transform.localScale = new Vector3(3f, 0.8f, 3f);
            cap.GetComponent<Renderer>().sharedMaterial = a.Paint(new Color(0.16f, 0.17f, 0.19f), 0.7f, 0.4f);
            Refresh();
        }

        static Mesh RingMesh(float inner, float outer, int segments)
        {
            var verts = new Vector3[segments * 2];
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float ang = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                verts[i * 2] = dir * inner;
                verts[i * 2 + 1] = dir * outer;
                int n = (i + 1) % segments;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = n * 2; tris[t + 2] = i * 2 + 1;
                tris[t + 3] = i * 2 + 1; tris[t + 4] = n * 2; tris[t + 5] = n * 2 + 1;
            }
            var m = new Mesh { name = "ZoneRing", vertices = verts, triangles = tris };
            var normals = new Vector3[verts.Length];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            m.normals = normals;
            m.RecalculateBounds();
            return m;
        }

        void Update()
        {
            if (netDriven) return;
            int c0 = 0, c1 = 0;
            Vector3 p = transform.position;
            foreach (var d in Damageable.All)
            {
                if (d == null || d.IsDead || d.GetComponent<Locomotion>() == null) continue;
                Vector3 off = d.transform.position - p;
                off.y = 0f;
                if (off.sqrMagnitude > radius * radius) continue;
                if (d.team == 0) c0++; else c1++;
            }
            Count0 = c0;
            Count1 = c1;

            float rate = Time.deltaTime / captureTime;
            if (c0 > 0 && c1 == 0) Progress = Mathf.Min(1f, Progress + rate * (1f + 0.3f * (c0 - 1)));
            else if (c1 > 0 && c0 == 0) Progress = Mathf.Max(-1f, Progress - rate * (1f + 0.3f * (c1 - 1)));

            int owner = Owner;
            if (Progress >= 1f) owner = 0;
            else if (Progress <= -1f) owner = 1;
            else if (Owner == 0 && Progress <= 0f) owner = -1;
            else if (Owner == 1 && Progress >= 0f) owner = -1;
            if (owner != Owner)
            {
                Owner = owner;
                if (owner >= 0)
                {
                    Captured?.Invoke(this, owner);
                    GameAudio.Play(Sfx.Confirm, p, 1f, 1f);
                }
            }
            Refresh();
        }

        void Refresh()
        {
            if (ring == null || shown == Owner) return;
            shown = Owner;
            var m = Owner == 0 ? allyMat : Owner == 1 ? enemyMat : neutralMat;
            ring.sharedMaterial = m;
            if (beacon != null) beacon.sharedMaterial = m;
        }
    }

    /// <summary>Mission buildings: enemy fuel depots to destroy and your reactor to defend.</summary>
    public static class Structures
    {
        static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        static Damageable Finish(GameObject go, int team, string name, float health, float size, Vector3 smokeAt)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.mass = 50000f;
            var smoke = new GameObject("Smoke").transform;
            smoke.SetParent(go.transform, false);
            smoke.localPosition = smokeAt;
            var d = go.AddComponent<Damageable>();
            d.team = team;
            d.displayName = name;
            d.maxHealth = health;
            d.Heal(health);
            d.size = size;
            d.smokePoint = smoke;
            d.impactThreshold = 30f;
            return d;
        }

        public static Damageable FuelDepot(Vector3 pos, float yaw)
        {
            var a = GameAssets.Get();
            var go = new GameObject("Fuel Depot");
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var t = go.transform;
            var concrete = a.floorMat != null ? a.floorMat : a.Paint(new Color(0.4f, 0.4f, 0.4f), 0f, 0.3f, false);
            var tank = a.Paint(new Color(0.55f, 0.22f, 0.12f), 0.6f, 0.45f);
            var steel = a.Paint(new Color(0.3f, 0.32f, 0.34f), 0.8f, 0.5f);
            var hazard = a.hazardMat != null ? a.hazardMat : a.Paint(UiKit.Yellow, 0.2f, 0.4f, false);
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.5f, 0f), new Vector3(20f, 1f, 12f), concrete);
            for (int i = -1; i <= 1; i += 2)
            {
                Part(PrimitiveType.Cylinder, t, new Vector3(i * 5f, 6f, 0f), new Vector3(7.5f, 5f, 7.5f), tank);
                Part(PrimitiveType.Cylinder, t, new Vector3(i * 5f, 11.2f, 0f), new Vector3(6.5f, 0.3f, 6.5f), steel);
                Part(PrimitiveType.Cylinder, t, new Vector3(i * 5f, 3f, 0f), new Vector3(7.7f, 0.6f, 7.7f), hazard);
            }
            Part(PrimitiveType.Cube, t, new Vector3(0f, 8f, 0f), new Vector3(3f, 0.8f, 0.8f), steel);
            Part(PrimitiveType.Cube, t, new Vector3(0f, 2.5f, 4.8f), new Vector3(12f, 0.6f, 0.6f), steel);
            var lamp = Part(PrimitiveType.Cube, t, new Vector3(0f, 9.2f, 0f), new Vector3(0.8f, 1f, 0.8f), a.Glow(UiKit.Red, 3f));
            Object.Destroy(lamp.GetComponent<Collider>());
            return Finish(go, 1, "FUEL DEPOT", 1400f, 2.2f, new Vector3(0f, 9f, 0f));
        }

        public static Damageable Reactor(Vector3 pos)
        {
            var a = GameAssets.Get();
            var go = new GameObject("Reactor");
            go.transform.position = pos;
            var t = go.transform;
            var steel = a.Paint(new Color(0.24f, 0.27f, 0.3f), 0.85f, 0.5f);
            var plate = a.Paint(new Color(0.75f, 0.76f, 0.72f), 0.4f, 0.5f);
            var hazard = a.hazardMat != null ? a.hazardMat : a.Paint(UiKit.Yellow, 0.2f, 0.4f, false);
            var core = a.Glow(UiKit.Cyan, 2.6f);
            Part(PrimitiveType.Cylinder, t, new Vector3(0f, 1f, 0f), new Vector3(18f, 1f, 18f), steel);
            Part(PrimitiveType.Cylinder, t, new Vector3(0f, 2.2f, 0f), new Vector3(16f, 0.25f, 16f), hazard);
            var c = Part(PrimitiveType.Cylinder, t, new Vector3(0f, 8f, 0f), new Vector3(4.5f, 6f, 4.5f), core);
            c.name = "Core";
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f;
                Vector3 o = Quaternion.Euler(0f, ang, 0f) * new Vector3(0f, 0f, 5.2f);
                Part(PrimitiveType.Cube, t, o + new Vector3(0f, 8f, 0f), new Vector3(1.6f, 12f, 1.2f), plate, ang);
            }
            Part(PrimitiveType.Cylinder, t, new Vector3(0f, 14.4f, 0f), new Vector3(12f, 0.8f, 12f), steel);
            Part(PrimitiveType.Cylinder, t, new Vector3(0f, 15.4f, 0f), new Vector3(5f, 1.2f, 5f), plate);
            return Finish(go, 0, "REACTOR", 5000f, 2.6f, new Vector3(0f, 15f, 0f));
        }
    }
}
