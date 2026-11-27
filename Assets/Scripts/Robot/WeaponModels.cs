using UnityEngine;

namespace RobotArena
{
    public static partial class RobotFactory
    {
        /// <summary>
        /// Places a Blender-made weapon model (Resources/RobotModels/Weapon&lt;name&gt;.bytes,
        /// made by Tools/mechs/weapons.py) on a mount. The model is built for the right-hand
        /// mount at scale 1; side &lt; 0 mirrors it, k scales it. Its 'Spin' part (Gatling
        /// barrels) goes under the given spin group so the weapon can turn it.
        /// Returns false when the build has no such model (the primitive weapon is used then).
        /// The parts keep their per-material submeshes, so they sit in their own group and are
        /// never passed to MeshMerge.
        /// </summary>
        static bool WeaponModel(Transform parent, string name, float k, float side = 1f, Transform spin = null)
        {
            var m = RobotModel.Load("Weapon" + name);
            if (m == null || m.info == null || m.info.frames == null) return false;
            var mats = new[] { mPrimary, mSecondary, mDark, mMetal, mChrome, mAccent, mGlow, mBlack, mRubber, mHot };

            var model = Group(parent, "GunModel", Vector3.zero);
            model.localScale = new Vector3(side < 0f ? -k : k, k, k);
            Transform spinModel = null;
            if (spin != null)
            {
                spinModel = Group(spin, "SpinModel", Vector3.zero);
                spinModel.localScale = new Vector3(k, k, k);
            }

            foreach (var f in m.info.frames)
            {
                if (!m.parts.TryGetValue(f.name, out var part)) continue;
                bool spins = f.name == "Spin" && spinModel != null;
                var go = new GameObject(f.name);
                var t = go.transform;
                t.SetParent(spins ? spinModel : model, false);
                // A spinning part's frame origin is its spin axis, where the spin group sits.
                t.localPosition = spins ? Vector3.zero : f.Origin;
                t.localRotation = f.Rotation;
                go.AddComponent<MeshFilter>().sharedMesh = part.mesh;
                var r = go.AddComponent<MeshRenderer>();
                var list = new Material[part.materials.Length];
                for (int i = 0; i < list.Length; i++) list[i] = mats[Mathf.Clamp(part.materials[i], 0, mats.Length - 1)];
                r.sharedMaterials = list;
            }
            return true;
        }
    }
}
