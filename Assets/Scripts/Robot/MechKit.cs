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
            P(g, Shape.Box, Vector3.zero, Vector3.zero, size, face);
            float inset = Mathf.Min(size.x, size.y) * 0.14f;
            Vector3 inner = new Vector3(Mathf.Max(0.1f, size.x - inset * 2f), Mathf.Max(0.1f, size.y - inset * 2f), Mathf.Min(0.16f, size.z * 0.35f));
            P(g, Shape.Box, new Vector3(0f, 0f, size.z * 0.5f + inner.z * 0.4f), Vector3.zero, inner, face);
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

        /// <summary>A hinge: drum along local X with dark caps, a bright hub and a ring of bolts.</summary>
        static Transform Joint(Transform parent, Vector3 pos, float diameter, float width, Material drum = null)
        {
            var g = Group(parent, "Joint", pos);
            drum = drum ?? mDark;
            P(g, Shape.Cylinder, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(diameter, width, diameter), drum);
            for (int s = -1; s <= 1; s += 2)
            {
                P(g, Shape.Cylinder, new Vector3(s * width * 0.5f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(diameter * 1.08f, 0.12f, diameter * 1.08f), mBlack);
                P(g, Shape.Cylinder, new Vector3(s * (width * 0.5f + 0.08f), 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(diameter * 0.55f, 0.1f, diameter * 0.55f), mMetal);
                for (int k = 0; k < 6; k++)
                {
                    float a = k * 60f * Mathf.Deg2Rad;
                    P(g, Shape.Cylinder, new Vector3(s * (width * 0.5f + 0.07f), Mathf.Cos(a) * diameter * 0.38f, Mathf.Sin(a) * diameter * 0.38f),
                        new Vector3(0f, 0f, 90f), new Vector3(diameter * 0.09f, 0.08f, diameter * 0.09f), mMetal);
                }
            }
            return g;
        }

        /// <summary>A hydraulic ram between two points: dark sleeve, chrome rod, end eyes.</summary>
        static void Piston(Transform parent, Vector3 a, Vector3 b, float radius)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.05f) return;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, d / len);
            var g = Group(parent, "Piston", a);
            g.localRotation = rot;
            P(g, Shape.Cylinder, new Vector3(0f, len * 0.3f, 0f), Vector3.zero, new Vector3(radius * 2f, len * 0.58f, radius * 2f), mDark);
            P(g, Shape.Cylinder, new Vector3(0f, len * 0.58f, 0f), Vector3.zero, new Vector3(radius * 2.3f, 0.12f, radius * 2.3f), mBlack);
            P(g, Shape.Cylinder, new Vector3(0f, len * 0.75f, 0f), Vector3.zero, new Vector3(radius * 1.1f, len * 0.5f, radius * 1.1f), mMetal);
            P(g, Shape.Sphere, Vector3.zero, Vector3.zero, Vector3.one * radius * 2.4f, mDark);
            P(g, Shape.Sphere, new Vector3(0f, len, 0f), Vector3.zero, Vector3.one * radius * 2.2f, mDark);
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
            P(g, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(w, h, d), mSecondary);
            P(g, Shape.Box, new Vector3(0f, h * 0.42f, d * 0.05f), new Vector3(-12f, 0f, 0f), new Vector3(w * 1.06f, h * 0.3f, d * 0.95f), mPrimary);
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
        /// </summary>
        static void LegSegment(Transform seg, float length, float width, float depth, float side, bool shin)
        {
            // Inner structure
            P(seg, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(width * 0.7f, depth * 0.7f, length * 0.92f), mDark);
            // Front armour shell, two overlapping plates
            Armor(seg, new Vector3(0f, depth * 0.38f, -length * 0.14f), new Vector3(-90f, 0f, 0f), new Vector3(width, length * 0.5f, depth * 0.3f), mPrimary);
            Armor(seg, new Vector3(0f, depth * 0.42f, length * 0.2f), new Vector3(-90f, 0f, 0f), new Vector3(width * 0.92f, length * 0.42f, depth * 0.26f), mPrimary, mDark, false);
            // Side plates
            for (int s = -1; s <= 1; s += 2)
                P(seg, Shape.Box, new Vector3(s * width * 0.42f, 0f, 0f), Vector3.zero, new Vector3(0.18f, depth * 0.75f, length * 0.7f), mSecondary);
            // Hydraulics at the back
            Piston(seg, new Vector3(-side * width * 0.15f, -depth * 0.42f, -length * 0.42f), new Vector3(-side * width * 0.15f, -depth * 0.42f, length * 0.38f), Mathf.Max(0.12f, width * 0.1f));
            Piston(seg, new Vector3(side * width * 0.2f, -depth * 0.36f, -length * 0.38f), new Vector3(side * width * 0.2f, -depth * 0.36f, length * 0.3f), Mathf.Max(0.1f, width * 0.08f));
            // Accent stripe and a marker light
            P(seg, Shape.Box, new Vector3(0f, depth * 0.62f, length * (shin ? 0.3f : -0.3f)), Vector3.zero, new Vector3(width * 0.5f, 0.06f, 0.22f), mAccent);
        }

        /// <summary>Heavy mech foot: sole, two front toes, a heel spur and an ankle joint. Faces +Z.</summary>
        static void MechFoot(Transform foot, float w, float len, Material toeMat)
        {
            // The foot's origin is the ankle, about 0.35 m above the ground.
            P(foot, Shape.Box, new Vector3(0f, -0.12f, len * 0.06f), Vector3.zero, new Vector3(w * 0.9f, 0.46f, len * 0.62f), mDark);
            Armor(foot, new Vector3(0f, 0.22f, len * 0.02f), new Vector3(-75f, 0f, 0f), new Vector3(w * 0.78f, len * 0.5f, 0.4f), mSecondary, mDark, false);
            for (int s = -1; s <= 1; s += 2)
            {
                var toe = Group(foot, "Toe", new Vector3(s * w * 0.24f, -0.12f, len * 0.36f));
                toe.localRotation = Quaternion.Euler(0f, s * 10f, 0f);
                P(toe, Shape.Box, new Vector3(0f, 0f, len * 0.12f), Vector3.zero, new Vector3(w * 0.4f, 0.46f, len * 0.3f), toeMat);
                P(toe, Shape.Wedge, new Vector3(0f, -0.02f, len * 0.31f), Vector3.zero, new Vector3(w * 0.38f, 0.42f, len * 0.14f), mMetal);
                P(toe, Shape.Box, new Vector3(0f, 0.26f, len * 0.08f), Vector3.zero, new Vector3(w * 0.3f, 0.08f, len * 0.2f), mAccent);
            }
            P(foot, Shape.Wedge, new Vector3(0f, -0.14f, -len * 0.38f), new Vector3(0f, 180f, 0f), new Vector3(w * 0.5f, 0.42f, len * 0.22f), toeMat);
            Joint(foot, new Vector3(0f, 0.3f, 0f), w * 0.4f, w * 0.48f);
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
