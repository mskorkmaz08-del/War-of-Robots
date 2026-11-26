using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Building blocks for the robots: layered armour panels, joints with bolted
    /// caps, hydraulic pistons, vents, lights and cockpits. Everything is made of
    /// chamfered plates so the machines read as heavy engineered armour rather than
    /// plain boxes. Parts are merged into a few meshes once a robot is built.
    /// </summary>
    public static partial class RobotFactory
    {
        // Sizes of leg segments and feet built so far, so MergeLegs can fit the working rams.
        static readonly System.Collections.Generic.Dictionary<Transform, Vector3> segDims = new System.Collections.Generic.Dictionary<Transform, Vector3>();
        static readonly System.Collections.Generic.Dictionary<Transform, Vector2> footDims = new System.Collections.Generic.Dictionary<Transform, Vector2>();

        /// <summary>
        /// A sculpted armour shell: rounded edges (radius in metres, -1 = automatic) and
        /// optionally narrower towards its local +Y end.
        /// </summary>
        static Transform Shell(Transform parent, Vector3 pos, Vector3 euler, Vector3 size, Material mat, float radius = -1f, float taperX = 1f, float taperZ = 1f)
        {
            var go = new GameObject("Shell");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            float r = radius >= 0f ? radius : Mathf.Min(Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.3f, 0.45f);
            go.AddComponent<MeshFilter>().sharedMesh = Shapes.RoundedBox(new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)), Mathf.Max(0.02f, r), taperX, taperZ);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        /// <summary>A machined disc (axis = local Y after euler): chamfered rim, recessed ring, raised hub.</summary>
        static Transform DiscPart(Transform parent, Vector3 pos, Vector3 euler, float diameter, float thickness, Material mat)
        {
            var go = new GameObject("Disc");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            // Small discs don't need the machined profile: a plain cylinder is far cheaper.
            if (diameter < 1.1f)
            {
                go.transform.localScale = new Vector3(diameter, thickness, diameter);
                go.AddComponent<MeshFilter>().sharedMesh = Shapes.Get(Shape.Cylinder);
            }
            else
                go.AddComponent<MeshFilter>().sharedMesh = Shapes.Disc(diameter, Mathf.Max(0.06f, thickness));
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        /// <summary>A flexible hydraulic hose through the given local points.</summary>
        static void Hose(Transform parent, Vector3[] pts, float radius, Material mat = null)
        {
            var go = new GameObject("Hose");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Shapes.Tube(pts, radius);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat ?? mBlack;
        }

        /// <summary>
        /// A working ram from a point on one moving part to a point on another (local
        /// coordinates). restLength is the anchors' distance in a typical pose; the rod
        /// can slide from about 0.6x to 1.25x of it.
        /// </summary>
        static void Ram(Transform root, Transform boneA, Vector3 localA, Transform boneB, Vector3 localB, float radius, float restLength)
        {
            var a = Group(boneA, "RamA", localA);
            var b = Group(boneB, "RamB", localB);
            float sl = restLength * 0.62f, rl = restLength * 0.66f;
            var sleeve = Group(root, "RamSleeve", Vector3.zero);
            DiscPart(sleeve, Vector3.zero, new Vector3(0f, 0f, 90f), radius * 2.8f, radius * 1.6f, mDark);
            P(sleeve, Shape.Cylinder, new Vector3(0f, 0f, sl * 0.5f), new Vector3(90f, 0f, 0f), new Vector3(radius * 2f, sl, radius * 2f), mDark);
            for (int k = 0; k < 3; k++)
                P(sleeve, Shape.Cylinder, new Vector3(0f, 0f, sl * (0.15f + k * 0.3f)), new Vector3(90f, 0f, 0f), new Vector3(radius * 2.3f, 0.07f, radius * 2.3f), mDark);
            P(sleeve, Shape.Cylinder, new Vector3(0f, 0f, sl - 0.06f), new Vector3(90f, 0f, 0f), new Vector3(radius * 2.5f, 0.14f, radius * 2.5f), mDark);
            var rod = Group(root, "RamRod", Vector3.zero);
            P(rod, Shape.Cylinder, new Vector3(0f, 0f, -rl * 0.5f), new Vector3(90f, 0f, 0f), new Vector3(radius * 1.1f, rl, radius * 1.1f), mChrome);
            DiscPart(rod, Vector3.zero, new Vector3(0f, 0f, 90f), radius * 2.3f, radius * 1.4f, mChrome);
            MeshMerge.Merge(sleeve);
            MeshMerge.Merge(rod);
            var ram = sleeve.gameObject.AddComponent<HydraulicRam>();
            ram.anchorA = a;
            ram.anchorB = b;
            ram.sleeve = sleeve;
            ram.rod = rod;
        }

        /// <summary>Knee and ankle rams for every leg built with LegSegment and MechFoot.</summary>
        static void AddLegRams(List<WalkerLocomotion.Leg> legs)
        {
            foreach (var leg in legs)
            {
                if (leg.thigh == null || leg.shin == null) continue;
                if (!segDims.TryGetValue(leg.thigh, out var th) || !segDims.TryGetValue(leg.shin, out var sh)) continue;
                Transform root = leg.thigh.parent;
                float s = leg.side;
                // Knee ram on the outer side: upper thigh to upper shin.
                Vector3 la = new Vector3(s * th.y * 0.74f, th.z * 0.05f, -th.x * 0.22f);
                Vector3 lb = new Vector3(s * sh.y * 0.72f, -sh.z * 0.08f, -sh.x * 0.2f);
                float rest = Mathf.Sqrt((la.y - lb.y) * (la.y - lb.y) + Mathf.Pow(th.x * 0.5f + sh.x * 0.5f + lb.z - la.z, 2f)) * 0.95f;
                Ram(root, leg.thigh, la, leg.shin, lb, Mathf.Max(0.14f, th.y * 0.11f), rest);
                // Ankle ram at the back: lower shin to the heel.
                if (leg.foot != null && footDims.TryGetValue(leg.foot, out var fd))
                {
                    Vector3 sa = new Vector3(0f, -sh.z * 0.62f, sh.x * 0.08f);
                    Vector3 fb = new Vector3(0f, 0.45f, -fd.y * 0.34f);
                    float r2 = Mathf.Sqrt(Mathf.Pow(sh.x * 0.42f, 2f) + Mathf.Pow(fd.y * 0.34f - sh.z * 0.62f, 2f));
                    Ram(root, leg.shin, sa, leg.foot, fb, Mathf.Max(0.12f, sh.y * 0.09f), Mathf.Max(0.8f, r2));
                }
            }
        }

        /// <summary>A chamfered plate.</summary>
        static Transform Plate(Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3 euler = default)
        {
            return P(parent, Shape.Box, pos, euler, size, mat);
        }

        /// <summary>
        /// An armour panel facing local +Z of its own frame (set by euler): a thick
        /// base plate, a raised inner panel, a recessed seam and two bolts.
        /// </summary>
        static Transform Armor(Transform parent, Vector3 pos, Vector3 euler, Vector3 size, Material face, Material trim = null, bool bolts = true)
        {
            var g = Group(parent, "Armor", pos);
            g.localRotation = Quaternion.Euler(euler);
            trim = trim ?? mDark;
            Shell(g, Vector3.zero, Vector3.zero, size, face, Mathf.Min(size.z * 0.45f, 0.22f));
            float inset = Mathf.Min(size.x, size.y) * 0.14f;
            Vector3 inner = new Vector3(Mathf.Max(0.1f, size.x - inset * 2f), Mathf.Max(0.1f, size.y - inset * 2f), Mathf.Min(0.16f, size.z * 0.35f));
            Shell(g, new Vector3(0f, 0f, size.z * 0.5f + inner.z * 0.4f), Vector3.zero, inner, face, inner.z * 0.45f);
            // Seam line across the panel
            if (size.y > 1.2f)
                P(g, Shape.Box, new Vector3(0f, -size.y * 0.18f, size.z * 0.5f + inner.z * 0.85f), Vector3.zero, new Vector3(inner.x * 1.02f, 0.06f, 0.04f), trim);
            if (bolts && size.x > 0.9f && size.y > 0.6f)
            {
                for (int s = -1; s <= 1; s += 2)
                    P(g, Shape.Cylinder, new Vector3(s * (size.x * 0.5f - inset * 0.55f), size.y * 0.5f - inset * 0.55f, size.z * 0.5f + 0.02f),
                        new Vector3(90f, 0f, 0f), new Vector3(0.16f, 0.06f, 0.16f), mMetal);
            }
            return g;
        }

        /// <summary>
        /// A heavy actuator joint along local X: a dark drum between two big machined
        /// discs (chamfered rims, recessed ring, raised hub), a chrome hub cap and a ring of bolts.
        /// </summary>
        static Transform Joint(Transform parent, Vector3 pos, float diameter, float width, Material drum = null)
        {
            var g = Group(parent, "Joint", pos);
            drum = drum ?? mDark;
            P(g, Shape.Cylinder, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(diameter * 0.82f, width, diameter * 0.82f), drum);
            float thick = Mathf.Clamp(width * 0.22f, 0.18f, 0.6f);
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (width * 0.5f - thick * 0.3f);
                DiscPart(g, new Vector3(x, 0f, 0f), new Vector3(0f, 0f, 90f), diameter, thick, mSecondary);
                DiscPart(g, new Vector3(x + s * (thick * 0.5f + 0.04f), 0f, 0f), new Vector3(0f, 0f, 90f), diameter * 0.42f, 0.16f, mChrome);
                P(g, Shape.Cylinder, new Vector3(x + s * thick * 0.45f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(diameter * 0.66f, 0.05f, diameter * 0.66f), mBlack);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * 45f * Mathf.Deg2Rad;
                    P(g, Shape.Cylinder, new Vector3(x + s * (thick * 0.5f + 0.02f), Mathf.Cos(a) * diameter * 0.39f, Mathf.Sin(a) * diameter * 0.39f),
                        new Vector3(0f, 0f, 90f), new Vector3(diameter * 0.065f, 0.08f, diameter * 0.065f), mMetal);
                }
            }
            return g;
        }

        /// <summary>A hydraulic ram between two fixed points: banded sleeve, chrome rod, clevis eyes.</summary>
        static void Piston(Transform parent, Vector3 a, Vector3 b, float radius)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.05f) return;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, d / len);
            var g = Group(parent, "Piston", a);
            g.localRotation = rot;
            P(g, Shape.Cylinder, new Vector3(0f, len * 0.3f, 0f), Vector3.zero, new Vector3(radius * 2f, len * 0.56f, radius * 2f), mDark);
            for (int k = 0; k < 2; k++)
                P(g, Shape.Cylinder, new Vector3(0f, len * (0.12f + k * 0.34f), 0f), Vector3.zero, new Vector3(radius * 2.25f, 0.08f, radius * 2.25f), mBlack);
            P(g, Shape.Cylinder, new Vector3(0f, len * 0.585f, 0f), Vector3.zero, new Vector3(radius * 2.4f, 0.14f, radius * 2.4f), mSecondary);
            P(g, Shape.Cylinder, new Vector3(0f, len * 0.76f, 0f), Vector3.zero, new Vector3(radius * 1.05f, len * 0.46f, radius * 1.05f), mChrome);
            DiscPart(g, Vector3.zero, new Vector3(0f, 0f, 90f), radius * 2.6f, radius * 1.4f, mDark);
            DiscPart(g, new Vector3(0f, len, 0f), new Vector3(0f, 0f, 90f), radius * 2.3f, radius * 1.3f, mDark);
        }

        /// <summary>A vent grille facing local +Z (of euler): frame and dark slats.</summary>
        static void Vent(Transform parent, Vector3 pos, Vector3 euler, float w, float h, int slats = 4)
        {
            var g = Group(parent, "Vent", pos);
            g.localRotation = Quaternion.Euler(euler);
            P(g, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(w, h, 0.18f), mDark);
            float step = h / (slats + 1);
            for (int i = 1; i <= slats; i++)
                P(g, Shape.Box, new Vector3(0f, -h * 0.5f + step * i, 0.08f), new Vector3(-25f, 0f, 0f), new Vector3(w * 0.88f, step * 0.45f, 0.08f), mBlack);
        }

        /// <summary>A small lamp housing with a glowing lens, facing local +Z of euler.</summary>
        static void Lamp(Transform parent, Vector3 pos, Vector3 euler, float size, Material glow)
        {
            var g = Group(parent, "Lamp", pos);
            g.localRotation = Quaternion.Euler(euler);
            P(g, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(size * 1.3f, size, size * 0.6f), mDark);
            P(g, Shape.Box, new Vector3(0f, 0f, size * 0.3f), Vector3.zero, new Vector3(size, size * 0.62f, 0.06f), glow);
        }

        /// <summary>
        /// Cockpit head: armoured block, sloped brow, a glowing visor slit, cheek
        /// plates and side sensors. Faces +Z.
        /// </summary>
        static Transform Cockpit(Transform parent, Vector3 pos, float w, float h, float d)
        {
            var g = Group(parent, "Cockpit", pos);
            Shell(g, Vector3.zero, Vector3.zero, new Vector3(w, h, d), mSecondary, Mathf.Min(h, w) * 0.28f, 0.88f, 0.9f);
            Shell(g, new Vector3(0f, h * 0.42f, d * 0.05f), new Vector3(-12f, 0f, 0f), new Vector3(w * 1.06f, h * 0.3f, d * 0.95f), mPrimary, h * 0.12f, 0.9f, 0.9f);
            P(g, Shape.Box, new Vector3(0f, h * 0.05f, d * 0.5f), Vector3.zero, new Vector3(w * 0.84f, h * 0.24f, 0.12f), mBlack);
            P(g, Shape.Box, new Vector3(0f, h * 0.05f, d * 0.5f + 0.05f), Vector3.zero, new Vector3(w * 0.74f, h * 0.12f, 0.06f), mGlow);
            P(g, Shape.Box, new Vector3(0f, -h * 0.32f, d * 0.46f), new Vector3(20f, 0f, 0f), new Vector3(w * 0.6f, h * 0.3f, 0.3f), mDark);
            for (int s = -1; s <= 1; s += 2)
            {
                P(g, Shape.Box, new Vector3(s * w * 0.48f, -h * 0.05f, d * 0.2f), new Vector3(0f, s * 18f, 0f), new Vector3(0.22f, h * 0.8f, d * 0.7f), mPrimary);
                P(g, Shape.Cylinder, new Vector3(s * (w * 0.5f + 0.18f), h * 0.15f, -d * 0.15f), new Vector3(0f, 0f, 90f), new Vector3(h * 0.38f, 0.3f, h * 0.38f), mMetal);
            }
            return g;
        }

        /// <summary>A whip antenna with a base and a glowing tip.</summary>
        static Transform Antenna(Transform parent, Vector3 pos, float height)
        {
            var g = Group(parent, "Antenna", pos);
            P(g, Shape.Box, new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(0.45f, 0.25f, 0.45f), mDark);
            P(g, Shape.Cylinder, new Vector3(0f, height * 0.5f, 0f), Vector3.zero, new Vector3(0.1f, height, 0.1f), mMetal);
            P(g, Shape.Sphere, new Vector3(0f, height, 0f), Vector3.zero, Vector3.one * 0.24f, mGlow);
            return g;
        }

        /// <summary>
        /// Armoured leg segment, built along local Z (bone axis) with +Y toward the
        /// robot's front, which is how WalkerLocomotion orients thigh and shin groups.
        /// A sculpted shell over an exposed frame, a raised front plate, side armour,
        /// hoses at the back. The working rams across the knee and ankle are added in MergeLegs.
        /// </summary>
        static void LegSegment(Transform seg, float length, float width, float depth, float side, bool shin)
        {
            segDims[seg] = new Vector3(length, width, depth);
            // Exposed frame
            P(seg, Shape.Box, new Vector3(0f, -depth * 0.1f, 0f), Vector3.zero, new Vector3(width * 0.55f, depth * 0.6f, length * 0.96f), mDark);
            // Main sculpted shell: narrows towards the knee on the thigh, a heavy boot on the shin.
            float taper = shin ? 1.08f : 0.8f;
            Shell(seg, new Vector3(0f, depth * 0.1f, shin ? length * 0.04f : -length * 0.03f), new Vector3(90f, 0f, 0f),
                new Vector3(width * 1.12f, length * 0.84f, depth * 0.9f), mPrimary, -1f, taper, taper);
            // Raised front plate with seams
            Shell(seg, new Vector3(0f, depth * 0.55f, shin ? length * 0.02f : -length * 0.08f), new Vector3(90f, 0f, 0f),
                new Vector3(width * 0.76f, length * 0.56f, depth * 0.2f), mPrimary, 0.08f, shin ? 1.06f : 0.86f, 1f);
            for (int k = -1; k <= 1; k += 2)
                P(seg, Shape.Box, new Vector3(0f, depth * 0.655f, k * length * 0.17f), Vector3.zero, new Vector3(width * 0.7f, 0.04f, 0.05f), mBlack);
            // Side armour
            for (int s = -1; s <= 1; s += 2)
            {
                Shell(seg, new Vector3(s * width * 0.6f, -depth * 0.02f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.26f, length * 0.62f, depth * 0.7f), mSecondary, 0.1f);
                for (int k = 0; k < 3; k++)
                    P(seg, Shape.Box, new Vector3(s * width * 0.74f, depth * (0.2f - k * 0.14f), -length * 0.12f), Vector3.zero, new Vector3(0.05f, 0.06f, length * 0.16f), mBlack);
            }
            // Hoses down the back
            Hose(seg, new[] { new Vector3(side * width * 0.18f, -depth * 0.38f, -length * 0.46f), new Vector3(side * width * 0.3f, -depth * 0.52f, -length * 0.1f),
                new Vector3(side * width * 0.26f, -depth * 0.5f, length * 0.2f), new Vector3(side * width * 0.16f, -depth * 0.38f, length * 0.46f) }, Mathf.Max(0.07f, width * 0.05f));
            Hose(seg, new[] { new Vector3(-side * width * 0.14f, -depth * 0.38f, -length * 0.44f), new Vector3(-side * width * 0.2f, -depth * 0.48f, 0f),
                new Vector3(-side * width * 0.14f, -depth * 0.38f, length * 0.44f) }, Mathf.Max(0.05f, width * 0.035f));
            if (shin)
            {
                // Calf: armoured block with heat-sink fins
                Shell(seg, new Vector3(0f, -depth * 0.5f, -length * 0.12f), new Vector3(90f, 0f, 0f), new Vector3(width * 0.86f, length * 0.42f, depth * 0.36f), mSecondary, -1f, 0.9f, 1f);
                for (int k = 0; k < 4; k++)
                    P(seg, Shape.Box, new Vector3(0f, -depth * 0.7f, -length * 0.28f + k * length * 0.09f), Vector3.zero, new Vector3(width * 0.66f, 0.12f, 0.08f), mBlack);
            }
            // Accent stripe and a marker light
            P(seg, Shape.Box, new Vector3(0f, depth * 0.66f, length * (shin ? 0.3f : -0.32f)), Vector3.zero, new Vector3(width * 0.5f, 0.05f, 0.2f), mAccent);
            Lamp(seg, new Vector3(side * width * 0.62f, depth * 0.3f, length * (shin ? -0.34f : 0.3f)), new Vector3(0f, side * 90f, 0f), 0.22f, mGlow);
        }

        /// <summary>Heavy mech foot: sculpted sole, two armoured toes, a heel and an ankle joint. Faces +Z.</summary>
        static void MechFoot(Transform foot, float w, float len, Material toeMat)
        {
            footDims[foot] = new Vector2(w, len);
            // The foot's origin is the ankle, about 0.35 m above the ground.
            P(foot, Shape.Box, new Vector3(0f, -0.2f, len * 0.04f), Vector3.zero, new Vector3(w * 0.96f, 0.3f, len * 0.86f), mBlack);
            Shell(foot, new Vector3(0f, 0.02f, len * 0.02f), Vector3.zero, new Vector3(w * 0.86f, 0.6f, len * 0.62f), mDark, 0.22f, 0.82f, 0.86f);
            Shell(foot, new Vector3(0f, 0.32f, len * 0.08f), new Vector3(-14f, 0f, 0f), new Vector3(w * 0.66f, 0.3f, len * 0.42f), mSecondary, 0.12f, 0.9f, 0.8f);
            for (int s = -1; s <= 1; s += 2)
            {
                var toe = Group(foot, "Toe", new Vector3(s * w * 0.25f, -0.08f, len * 0.33f));
                toe.localRotation = Quaternion.Euler(0f, s * 8f, 0f);
                Shell(toe, new Vector3(0f, 0f, len * 0.12f), Vector3.zero, new Vector3(w * 0.42f, 0.52f, len * 0.32f), toeMat, 0.16f, 0.85f, 0.8f);
                P(toe, Shape.Wedge, new Vector3(0f, -0.08f, len * 0.31f), Vector3.zero, new Vector3(w * 0.38f, 0.36f, len * 0.12f), mMetal);
                P(toe, Shape.Box, new Vector3(0f, 0.28f, len * 0.1f), Vector3.zero, new Vector3(w * 0.26f, 0.06f, len * 0.16f), mAccent);
                Joint(toe, new Vector3(0f, 0.05f, -len * 0.04f), 0.42f, w * 0.36f);
            }
            Shell(foot, new Vector3(0f, -0.05f, -len * 0.36f), Vector3.zero, new Vector3(w * 0.5f, 0.5f, len * 0.22f), toeMat, 0.14f, 0.8f, 0.8f);
            Joint(foot, new Vector3(0f, 0.42f, 0f), w * 0.44f, w * 0.5f);
        }

        // ---- Human-scale details ----
        // Ladders, handrails, hatches and stencils are sized for a person. Next to
        // them the eye reads the machine as huge; without them it reads as a toy.

        /// <summary>A maintenance ladder from bottom upwards, facing local +Z after yaw.</summary>
        static void Ladder(Transform parent, Vector3 bottom, float height, float yaw = 0f)
        {
            var g = Group(parent, "Ladder", bottom);
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            for (int s = -1; s <= 1; s += 2)
                P(g, Shape.Box, new Vector3(s * 0.24f, height * 0.5f, 0.08f), Vector3.zero, new Vector3(0.06f, height, 0.06f), mMetal);
            for (float y = 0.25f; y < height - 0.1f; y += 0.34f)
                P(g, Shape.Box, new Vector3(0f, y, 0.08f), Vector3.zero, new Vector3(0.48f, 0.04f, 0.04f), mMetal);
        }

        /// <summary>A waist-high handrail around a w x d deck centred on pos.</summary>
        static void Railing(Transform parent, Vector3 pos, float w, float d)
        {
            var g = Group(parent, "Railing", pos);
            const float h = 1.0f;
            for (int s = -1; s <= 1; s += 2)
            {
                P(g, Shape.Box, new Vector3(0f, h, s * d * 0.5f), Vector3.zero, new Vector3(w, 0.05f, 0.05f), mAccent);
                P(g, Shape.Box, new Vector3(s * w * 0.5f, h, 0f), Vector3.zero, new Vector3(0.05f, 0.05f, d), mAccent);
                P(g, Shape.Box, new Vector3(0f, h * 0.5f, s * d * 0.5f), Vector3.zero, new Vector3(w, 0.03f, 0.03f), mMetal);
                for (float x = -w * 0.5f; x <= w * 0.5f + 0.01f; x += Mathf.Max(0.8f, w / 6f))
                    P(g, Shape.Box, new Vector3(x, h * 0.5f, s * d * 0.5f), Vector3.zero, new Vector3(0.05f, h, 0.05f), mMetal);
                for (float z = -d * 0.5f + 0.9f; z < d * 0.5f - 0.4f; z += Mathf.Max(0.8f, d / 4f))
                    P(g, Shape.Box, new Vector3(s * w * 0.5f, h * 0.5f, z), Vector3.zero, new Vector3(0.05f, h, 0.05f), mMetal);
            }
        }

        /// <summary>A crew hatch with hinges and a handle, on a surface facing local +Z after euler.</summary>
        static void Hatch(Transform parent, Vector3 pos, Vector3 euler, float size = 0.9f)
        {
            var g = Group(parent, "Hatch", pos);
            g.localRotation = Quaternion.Euler(euler);
            P(g, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(size, size * 1.2f, 0.08f), mSecondary);
            P(g, Shape.Box, new Vector3(0f, 0f, 0.06f), Vector3.zero, new Vector3(size * 0.35f, 0.05f, 0.06f), mMetal);
            for (int s = -1; s <= 1; s += 2)
                P(g, Shape.Box, new Vector3(-size * 0.5f, s * size * 0.35f, 0.04f), Vector3.zero, new Vector3(0.1f, 0.18f, 0.1f), mDark);
        }

        /// <summary>A painted stencil block: unit number bars and a hazard strip.</summary>
        static void Stencil(Transform parent, Vector3 pos, Vector3 euler, float w)
        {
            var g = Group(parent, "Stencil", pos);
            g.localRotation = Quaternion.Euler(euler);
            for (int i = 0; i < 3; i++)
                P(g, Shape.Box, new Vector3(-w * 0.3f + i * w * 0.16f, 0f, 0f), Vector3.zero, new Vector3(w * 0.06f, w * 0.22f, 0.02f), mAccent);
            for (int i = 0; i < 4; i++)
                P(g, Shape.Box, new Vector3(w * 0.12f + i * w * 0.09f, 0f, 0f), new Vector3(0f, 0f, 40f), new Vector3(w * 0.04f, w * 0.22f, 0.02f), i % 2 == 0 ? mAccent : mDark);
        }
    }
}
