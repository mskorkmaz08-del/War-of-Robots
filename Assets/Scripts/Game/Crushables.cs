using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Giant robots don't walk around cars, trees and houses: they walk through them.
    /// Every prop a robot steps on gets crushed in place inside its chunk mesh: cars are
    /// flattened and blackened, trees and lamp posts snap and fall away from the robot,
    /// houses cave in under a cloud of dust. Explosions do the same in their radius.
    /// </summary>
    public class Crushables : MonoBehaviour
    {
        public static Crushables Instance { get; private set; }

        public class Chunk
        {
            public Mesh mesh;
            public Vector3[] verts;
            public Color32[] cols;
            public bool dirty;
        }

        const float GridCell = 16f;

        Dictionary<long, Chunk> chunks;
        List<PropWorld.Crush> items;
        bool[] done;
        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        float nextTick;

        public void Init(Dictionary<long, Chunk> chunkMeshes, List<PropWorld.Crush> list)
        {
            Instance = this;
            chunks = chunkMeshes;
            items = list;
            done = new bool[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                long k = Cell(list[i].pivot.x, list[i].pivot.z);
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                l.Add(i);
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static long Cell(float x, float z) => (long)(Mathf.FloorToInt(x / GridCell) + 10000) * 100000 + (Mathf.FloorToInt(z / GridCell) + 10000);

        void Update()
        {
            if (items == null || Time.time < nextTick) return;
            nextTick = Time.time + 0.08f;
            foreach (var d in Damageable.All)
            {
                if (d == null || d.IsDead || d.Body == null || d.Body.isKinematic) continue;
                if (d.GetComponent<Locomotion>() == null) continue; // robots only, not depots
                Vector3 p = d.transform.position;
                float radius = Mathf.Max(3f, d.size * 1.9f);
                // Walkers are tall: only what's under their feet, not what they jump over.
                float ground = BattleGround.HeightAt(p.x, p.z);
                if (p.y - ground > d.size * 9f) continue;
                Visit(p, radius, (i) => Crush(i, p, false));
            }
            Flush();
        }

        void Visit(Vector3 p, float radius, System.Action<int> act)
        {
            float reach = radius + 8f;
            int x0 = Mathf.FloorToInt((p.x - reach) / GridCell), x1 = Mathf.FloorToInt((p.x + reach) / GridCell);
            int z0 = Mathf.FloorToInt((p.z - reach) / GridCell), z1 = Mathf.FloorToInt((p.z + reach) / GridCell);
            for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                long k = (long)(x + 10000) * 100000 + (z + 10000);
                if (!grid.TryGetValue(k, out var list)) continue;
                foreach (int i in list)
                {
                    if (done[i]) continue;
                    var it = items[i];
                    float dx = it.pivot.x - p.x, dz = it.pivot.z - p.z;
                    float rr = radius + it.radius;
                    if (dx * dx + dz * dz < rr * rr) act(i);
                }
            }
        }

        /// <summary>Explosion: everything in about two thirds of the blast radius goes over.</summary>
        public void Blast(Vector3 pos, float radius)
        {
            if (items == null) return;
            Visit(pos, radius * 0.65f, (i) => Crush(i, pos, true));
            Flush();
        }

        void Flush()
        {
            foreach (var c in chunks.Values)
            {
                if (!c.dirty) continue;
                c.dirty = false;
                c.mesh.vertices = c.verts;
                c.mesh.colors32 = c.cols;
                c.mesh.RecalculateNormals();
                c.mesh.RecalculateBounds();
            }
        }

        void Crush(int i, Vector3 from, bool blast)
        {
            done[i] = true;
            var it = items[i];
            if (!chunks.TryGetValue(it.chunk, out var ch)) return;
            ch.dirty = true;
            Vector3 pivot = it.pivot;
            Vector3 away = new Vector3(pivot.x - from.x, 0f, pivot.z - from.z);
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;
            away.Normalize();
            var v = ch.verts;
            var c = ch.cols;
            switch (it.kind)
            {
                case PropWorld.Kind.Car:
                case PropWorld.Kind.Truck:
                {
                    // Flattened to a third of its height, buckled, burnt black.
                    float squash = blast ? 0.6f : 0.32f;
                    float tilt = Random.Range(-0.12f, 0.12f);
                    for (int k = it.start; k < it.end; k++)
                    {
                        Vector3 q = v[k] - pivot;
                        q.y = q.y * squash + (q.x * tilt);
                        q.x *= 1.06f;
                        q.z *= 1.03f;
                        v[k] = pivot + q;
                        c[k] = Darken(c[k], blast ? 0.25f : 0.55f);
                    }
                    Fx.Sparks(pivot + Vector3.up, Vector3.up, 14, 9f, 80f);
                    if (it.kind == PropWorld.Kind.Truck || Random.value < 0.5f) GameAudio.Play(Sfx.MetalHit, pivot, 0.7f, Random.Range(0.55f, 0.7f));
                    break;
                }
                case PropWorld.Kind.Tree:
                case PropWorld.Kind.Pole:
                {
                    // Snaps at the base and falls away from the robot.
                    Vector3 axis = Vector3.Cross(Vector3.up, away);
                    var rot = Quaternion.AngleAxis(Random.Range(72f, 86f), axis);
                    Vector3 hinge = pivot + Vector3.up * 0.4f;
                    for (int k = it.start; k < it.end; k++)
                    {
                        Vector3 q = v[k] - hinge;
                        if (q.y < 0f) continue;
                        v[k] = hinge + rot * q;
                    }
                    Fx.Dust(pivot + away * it.height * 0.6f, 1.6f);
                    if (it.kind == PropWorld.Kind.Pole) GameAudio.Play(Sfx.MetalHit, pivot, 0.45f, Random.Range(0.8f, 1f));
                    break;
                }
                case PropWorld.Kind.House:
                {
                    // Caves in: walls spread and drop into a heap of rubble.
                    for (int k = it.start; k < it.end; k++)
                    {
                        Vector3 q = v[k] - pivot;
                        float h = Mathf.Max(0f, q.y);
                        float jitter = Mathf.Sin(k * 12.9898f) * 0.5f + 0.5f;
                        q.y = h * 0.16f + jitter * 0.9f;
                        q.x = q.x * 1.18f + away.x * h * 0.25f;
                        q.z = q.z * 1.18f + away.z * h * 0.25f;
                        v[k] = pivot + q;
                        c[k] = Darken(c[k], 0.62f);
                    }
                    Fx.Dust(pivot + Vector3.up * 2f, 7f);
                    Fx.Dust(pivot + away * 4f + Vector3.up, 5f);
                    GameAudio.Play(Sfx.Debris, pivot, 0.9f, Random.Range(0.7f, 0.85f));
                    FollowCamera.Shake(0.08f, pivot, 60f);
                    break;
                }
            }
        }

        static Color32 Darken(Color32 c, float k) => new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), (byte)(c.a * 0.3f));
    }
}
