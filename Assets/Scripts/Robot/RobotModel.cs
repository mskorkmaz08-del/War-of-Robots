using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// A sculpted robot model made in Blender (Tools/mechs/*.py) and stored as
    /// Resources/RobotModels/&lt;Type&gt;.bytes. Each part is a mesh (positions quantised to 16 bits) in the frame of
    /// the game bone that moves it, with one submesh per livery material.
    /// A JSON block holds where each part sits, where weapons and jets mount,
    /// and the hydraulic rams that link two moving parts.
    /// </summary>
    public class RobotModel
    {
        [System.Serializable]
        public class Frame
        {
            public string name;
            public string parent;
            public float[] o, x, y, z;
            public Vector3 Origin => V(o);
            public Quaternion Rotation => Quaternion.LookRotation(V(z), V(y));
        }

        [System.Serializable]
        public class Anchor
        {
            public string name;
            public string parent;
            public float[] p, f;
            public Vector3 Pos => V(p);
            public Vector3 Forward => V(f);
        }

        [System.Serializable]
        public class Ram
        {
            public string sleeve, rod, a, b;
            public float[] pa, pb;
        }

        [System.Serializable]
        public class LegInfo
        {
            public string name;
            public float[] footOffset, bend;
        }

        /// <summary>A part that keeps turning: radar dishes, cooling fans, sensor heads.</summary>
        [System.Serializable]
        public class Spin
        {
            public string name;
            public float[] axis;
            public float speed;   // degrees per second
        }

        [System.Serializable]
        public class Info
        {
            public float stand, thigh, shin, ankle, footOut, wheelRadius;
            public int wheels;
            public float[] upper, arms;
            public Frame[] frames;
            public Anchor[] anchors;
            public Ram[] rams;
            public LegInfo[] legs;
            public string[] detach;
            public Spin[] spin;
        }

        public class Part
        {
            public Mesh mesh;
            public int[] materials;   // index into the livery material list, per submesh
        }

        public Info info;
        public readonly Dictionary<string, Part> parts = new Dictionary<string, Part>();
        readonly Dictionary<string, Frame> frameByName = new Dictionary<string, Frame>();
        readonly Dictionary<string, Anchor> anchorByName = new Dictionary<string, Anchor>();

        static readonly Dictionary<string, RobotModel> cache = new Dictionary<string, RobotModel>();

        public static Vector3 V(float[] a) => a == null || a.Length < 3 ? Vector3.zero : new Vector3(a[0], a[1], a[2]);

        /// <summary>The model for this robot, or null if the build has none (the old builders are used then).</summary>
        public static RobotModel Load(string name) => Load(name, 1f);

        /// <summary>The model scaled up by k (battle robots are bigger than the hangar ones).</summary>
        public static RobotModel Load(string name, float k)
        {
            string key = k == 1f ? name : name + "@" + k.ToString("0.000");
            if (cache.TryGetValue(key, out var m)) return m;
            if (k != 1f)
            {
                var src = Load(name, 1f);
                m = src != null ? src.ScaledCopy(k) : null;
                cache[key] = m;
                return m;
            }
            m = null;
            try
            {
                var ta = Resources.Load<TextAsset>("RobotModels/" + name);
                if (ta != null) m = Parse(ta.bytes, name);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Robot model " + name + " failed to load: " + e.Message);
                m = null;
            }
            cache[name] = m;
            return m;
        }

        static RobotModel Parse(byte[] data, string name)
        {
            var m = new RobotModel();
            using (var br = new BinaryReader(new MemoryStream(data)))
            {
                var magic = new string(br.ReadChars(4));
                if (magic != "RBM3") throw new IOException("not a robot model");
                int count = br.ReadUInt16();
                for (int p = 0; p < count; p++)
                {
                    int nl = br.ReadByte();
                    string pn = System.Text.Encoding.UTF8.GetString(br.ReadBytes(nl));
                    int vc = (int)br.ReadUInt32();
                    var verts = new Vector3[vc];
                    var norms = new Vector3[vc];
                    var lo = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    var sz = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()) / 65535f;
                    for (int i = 0; i < vc; i++)
                        verts[i] = lo + new Vector3(br.ReadUInt16() * sz.x, br.ReadUInt16() * sz.y, br.ReadUInt16() * sz.z);
                    for (int i = 0; i < vc; i++)
                    {
                        float nx = br.ReadSByte() / 127f, ny = br.ReadSByte() / 127f, nz = br.ReadSByte() / 127f;
                        norms[i] = new Vector3(nx, ny, nz).normalized;
                    }
                    int subs = br.ReadByte();
                    bool wide = br.ReadByte() != 0;
                    var mesh = new Mesh { name = name + "_" + pn };
                    if (wide) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices = verts;
                    mesh.normals = norms;
                    mesh.subMeshCount = subs;
                    var mats = new int[subs];
                    for (int s = 0; s < subs; s++)
                    {
                        mats[s] = br.ReadByte();
                        int ic = (int)br.ReadUInt32();
                        var idx = new int[ic];
                        for (int i = 0; i < ic; i++) idx[i] = wide ? (int)br.ReadUInt32() : br.ReadUInt16();
                        mesh.SetTriangles(idx, s);
                    }
                    mesh.RecalculateBounds();
                    m.parts[pn] = new Part { mesh = mesh, materials = mats };
                }
                int jl = (int)br.ReadUInt32();
                string json = System.Text.Encoding.UTF8.GetString(br.ReadBytes(jl));
                m.info = JsonUtility.FromJson<Info>(json);
            }
            if (m.info.frames != null) foreach (var f in m.info.frames) m.frameByName[f.name] = f;
            if (m.info.anchors != null) foreach (var a in m.info.anchors) m.anchorByName[a.name] = a;
            return m;
        }

        /// <summary>
        /// A copy with every length multiplied by k: meshes, part and anchor positions,
        /// ram ends, leg lengths, stand height. Rotations stay as they are.
        /// </summary>
        RobotModel ScaledCopy(float k)
        {
            var c = new RobotModel();
            foreach (var kv in parts)
            {
                var src = kv.Value.mesh;
                var mesh = Object.Instantiate(src);
                mesh.name = src.name + "_x" + k.ToString("0.00");
                var v = mesh.vertices;
                for (int i = 0; i < v.Length; i++) v[i] *= k;
                mesh.vertices = v;
                mesh.RecalculateBounds();
                c.parts[kv.Key] = new Part { mesh = mesh, materials = kv.Value.materials };
            }
            // Deep copy of the info block through JSON, then scale the lengths.
            var info2 = JsonUtility.FromJson<Info>(JsonUtility.ToJson(info));
            float[] S(float[] a)
            {
                if (a == null) return null;
                var r = (float[])a.Clone();
                for (int i = 0; i < r.Length; i++) r[i] *= k;
                return r;
            }
            info2.stand *= k; info2.thigh *= k; info2.shin *= k; info2.ankle *= k; info2.footOut *= k; info2.wheelRadius *= k;
            info2.upper = S(info2.upper);
            info2.arms = S(info2.arms);
            if (info2.frames != null) foreach (var f in info2.frames) f.o = S(f.o);
            if (info2.anchors != null) foreach (var a in info2.anchors) a.p = S(a.p);
            if (info2.rams != null) foreach (var r in info2.rams) { r.pa = S(r.pa); r.pb = S(r.pb); }
            if (info2.legs != null) foreach (var l in info2.legs) l.footOffset = S(l.footOffset);
            c.info = info2;
            if (c.info.frames != null) foreach (var f in c.info.frames) c.frameByName[f.name] = f;
            if (c.info.anchors != null) foreach (var a in c.info.anchors) c.anchorByName[a.name] = a;
            return c;
        }

        public Frame GetFrame(string n) => frameByName.TryGetValue(n, out var f) ? f : null;
        public Anchor GetAnchor(string n) => anchorByName.TryGetValue(n, out var a) ? a : null;
        public Vector3 Upper => V(info.upper);
        public Vector3 Arms => V(info.arms);
    }

    public static partial class RobotFactory
    {
        /// <summary>
        /// Places every part of a sculpted model on the robot. Rigid parts hang off the
        /// root, the turning upper body or the pitching arms; leg parts become the bones
        /// the walker poses; rams get a HydraulicRam that slides them as the joints move.
        /// Returns the made transforms by part name; also fills the anchor transforms.
        /// </summary>
        static Dictionary<string, Transform> PlaceModel(RobotModel m, Transform root, Transform upper, Transform arms, Dictionary<string, Transform> anchorsOut)
        {
            var mats = new[] { mPrimary, mSecondary, mDark, mMetal, mChrome, mAccent, mGlow, mBlack, mRubber, mHot };
            var made = new Dictionary<string, Transform>();
            Vector3 rootOrigin = new Vector3(0f, m.info.stand, 0f);

            Transform ParentOf(string p)
            {
                if (p == "Upper") return upper;
                if (p == "Arms") return arms;
                return root;
            }

            foreach (var f in m.info.frames)
            {
                if (!m.parts.TryGetValue(f.name, out var part)) continue;
                var go = new GameObject(f.name);
                var t = go.transform;
                // Model space is the robot's own space with the ground at y = 0.
                t.SetParent(root, false);
                t.localPosition = f.Origin - rootOrigin;
                t.localRotation = f.Rotation;
                var parent = ParentOf(f.parent);
                if (parent != root) t.SetParent(parent, true);
                go.AddComponent<MeshFilter>().sharedMesh = part.mesh;
                var r = go.AddComponent<MeshRenderer>();
                var list = new Material[part.materials.Length];
                for (int i = 0; i < list.Length; i++) list[i] = mats[Mathf.Clamp(part.materials[i], 0, mats.Length - 1)];
                r.sharedMaterials = list;
                made[f.name] = t;
            }

            if (m.info.spin != null)
                foreach (var sp in m.info.spin)
                    if (made.TryGetValue(sp.name, out var st))
                    {
                        var spinner = st.gameObject.AddComponent<Spinner>();
                        spinner.axis = sp.axis != null && sp.axis.Length == 3 ? RobotModel.V(sp.axis) : Vector3.up;
                        spinner.speed = sp.speed;
                    }

            if (m.info.anchors != null)
                foreach (var a in m.info.anchors)
                {
                    var t = new GameObject(a.name).transform;
                    t.SetParent(root, false);
                    t.localPosition = a.Pos - rootOrigin;
                    if (a.Forward.sqrMagnitude > 0.001f) t.localRotation = Quaternion.LookRotation(a.Forward, Mathf.Abs(a.Forward.y) > 0.95f ? Vector3.forward : Vector3.up);
                    var parent = ParentOf(a.parent);
                    if (parent != root) t.SetParent(parent, true);
                    anchorsOut[a.name] = t;
                }

            if (m.info.rams != null)
                foreach (var ram in m.info.rams)
                {
                    if (!made.TryGetValue(ram.a, out var boneA) || !made.TryGetValue(ram.b, out var boneB)) continue;
                    var ea = new GameObject("RamEnd").transform;
                    ea.SetParent(boneA, false);
                    ea.localPosition = RobotModel.V(ram.pa);
                    var eb = new GameObject("RamEnd").transform;
                    eb.SetParent(boneB, false);
                    eb.localPosition = RobotModel.V(ram.pb);
                    made.TryGetValue(ram.sleeve, out var sleeve);
                    made.TryGetValue(ram.rod, out var rod);
                    var h = root.gameObject.AddComponent<HydraulicRam>();
                    h.anchorA = ea; h.anchorB = eb; h.sleeve = sleeve; h.rod = rod;
                }
            return made;
        }

        /// <summary>Walker legs from a model: bone parts Thigh/Knee/Shin/Foot + leg name, hips from anchors.</summary>
        static List<WalkerLocomotion.Leg> ModelLegs(RobotModel m, Dictionary<string, Transform> made, Dictionary<string, Transform> anchors)
        {
            var legs = new List<WalkerLocomotion.Leg>();
            var infos = m.info.legs;
            if (infos == null || infos.Length == 0)
                infos = new[] { new RobotModel.LegInfo { name = "L" }, new RobotModel.LegInfo { name = "R" } };
            foreach (var li in infos)
            {
                string sd = li.name;
                made.TryGetValue("Thigh" + sd, out var thigh);
                made.TryGetValue("Knee" + sd, out var knee);
                made.TryGetValue("Shin" + sd, out var shin);
                made.TryGetValue("Foot" + sd, out var foot);
                anchors.TryGetValue("Hip" + sd, out var hip);
                if (hip == null) continue;
                float side = hip.localPosition.x < 0f ? -1f : 1f;
                Vector3 off = li.footOffset != null ? RobotModel.V(li.footOffset) : new Vector3(side * m.info.footOut, 0f, 0f);
                Vector3 bend = li.bend != null ? RobotModel.V(li.bend) : Vector3.zero;
                // Forward-bending knees are the walker's default; only pass a hint when it differs.
                if ((bend - Vector3.forward).sqrMagnitude < 1e-4f) bend = Vector3.zero;
                legs.Add(new WalkerLocomotion.Leg { hip = hip, thigh = thigh, knee = knee, shin = shin, foot = foot, side = side, footOffset = off, bendHint = bend });
            }
            return legs;
        }

        static Transform AnchorOr(Dictionary<string, Transform> anchors, string n, Transform fallbackParent, Vector3 fallbackPos)
        {
            if (anchors.TryGetValue(n, out var t)) return t;
            return Group(fallbackParent, n, fallbackPos);
        }
    }
}
