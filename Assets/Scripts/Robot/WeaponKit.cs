using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// The attachment points a robot body offers for weapons. Arm mounts and the
    /// shoulder mount sit under the turret's pitch pivot (they aim up and down);
    /// back mounts sit under the yaw pivot (launchers that guide their own shots).
    /// Every mount faces local +Z.
    /// </summary>
    public class Chassis
    {
        public GameObject root;
        public Turret turret;
        public Transform[] armMounts = new Transform[0];
        public Transform shoulderMount;
        public Transform[] backMounts = new Transform[0];
        public float armScale = 1f;
        public float shoulderScale = 1f;
        public float backScale = 1f;
    }

    public static partial class RobotFactory
    {
        /// <summary>Builds the chosen weapons onto a robot body and adds their components.</summary>
        static void FitWeapons(Chassis c, Loadout loadout, int team, bool player)
        {
            FitPrimary(c, loadout.primary, team, player);
            FitSpecial(c, loadout.special, team);
        }

        static Transform Barrel(Transform mount)
        {
            // Weapons that kick back on firing move this group (the muzzle's parent).
            return Group(mount, "Barrel", Vector3.zero);
        }

        static void FitPrimary(Chassis c, WeaponId id, int team, bool player)
        {
            float k = c.armScale;
            var muzzles = new List<Transform>();
            var spinners = new List<Transform>();
            Transform charge = null;
            for (int i = 0; i < c.armMounts.Length; i++)
            {
                var m = c.armMounts[i];
                float side = m.localPosition.x >= 0f ? 1f : -1f;
                var b = Barrel(m);
                var vis = Group(b, "GunStatic", Vector3.zero);
                switch (id)
                {
                    case WeaponId.HeavyCannon:
                        if (!WeaponModel(b, "HeavyCannon", k, side))
                        {
                            Armor(vis, new Vector3(0f, 0f, 0.9f * k), new Vector3(0f, 0f, 0f), new Vector3(1.6f, 1.6f, 3.0f) * k, mPrimary);
                            P(vis, Shape.Box, new Vector3(0f, 0.95f * k, 0.6f * k), Vector3.zero, new Vector3(1.0f, 0.3f, 2.4f) * k, mDark);
                            P(vis, Shape.Box, new Vector3(side * 0.85f * k, 0f, 0.9f * k), Vector3.zero, new Vector3(0.12f, 0.7f, 2.2f) * k, mAccent);
                            P(vis, Shape.Cylinder, new Vector3(0f, 0f, 4.6f * k), new Vector3(90f, 0f, 0f), new Vector3(0.85f * k, 4.2f * k, 0.85f * k), mDark);
                            P(vis, Shape.Cylinder, new Vector3(0f, 0f, 3.2f * k), new Vector3(90f, 0f, 0f), new Vector3(1.15f * k, 0.3f * k, 1.15f * k), mGlow);
                            P(vis, Shape.Cylinder, new Vector3(0f, 0f, 4.4f * k), new Vector3(90f, 0f, 0f), new Vector3(1.1f * k, 0.25f * k, 1.1f * k), mMetal);
                            P(vis, Shape.Box, new Vector3(0f, 0f, 6.9f * k), Vector3.zero, new Vector3(1.45f, 1.0f, 0.95f) * k, mMetal);
                            P(vis, Shape.Box, new Vector3(0f, 0f, 6.9f * k), Vector3.zero, new Vector3(1.55f, 0.22f, 0.55f) * k, mBlack);
                        }
                        muzzles.Add(Group(b, "Muzzle", new Vector3(0f, 0f, 7.5f * k)));
                        break;
                    case WeaponId.PulseLaser:
                        if (!WeaponModel(b, "PulseLaser", k, side))
                        {
                            Armor(vis, new Vector3(0f, 0f, 0.7f * k), Vector3.zero, new Vector3(1.0f, 1.0f, 1.9f) * k, mSecondary);
                            for (int s = -1; s <= 1; s += 2)
                                P(vis, Shape.Box, new Vector3(s * 0.34f * k, 0f, 2.6f * k), Vector3.zero, new Vector3(0.18f, 0.6f, 2.6f) * k, mDark);
                            P(vis, Shape.Cylinder, new Vector3(0f, 0f, 2.6f * k), new Vector3(90f, 0f, 0f), new Vector3(0.36f * k, 2.8f * k, 0.36f * k), mMetal);
                            for (int r = 0; r < 3; r++)
                                P(vis, Shape.Cylinder, new Vector3(0f, 0f, (1.9f + r * 0.7f) * k), new Vector3(90f, 0f, 0f), new Vector3(0.75f * k, 0.14f * k, 0.75f * k), mGlow);
                            P(vis, Shape.Sphere, new Vector3(0f, 0f, 4.0f * k), Vector3.zero, Vector3.one * 0.45f * k, mGlow);
                        }
                        muzzles.Add(Group(b, "Muzzle", new Vector3(0f, 0f, 4.2f * k)));
                        break;
                    case WeaponId.Gatling:
                        var spin = Group(b, "Barrels", new Vector3(0f, 0f, 1.9f * k));
                        if (!WeaponModel(b, "Gatling", k, side, spin))
                        {
                            Armor(vis, new Vector3(0f, 0f, 0.6f * k), Vector3.zero, new Vector3(1.1f, 1.1f, 2.0f) * k, mSecondary);
                            P(vis, Shape.Box, new Vector3(0f, 0.68f * k, 0.5f * k), Vector3.zero, new Vector3(0.7f, 0.25f, 1.5f) * k, mAccent);
                            P(vis, Shape.Box, new Vector3(-side * 0.7f * k, -0.2f * k, 0.4f * k), Vector3.zero, new Vector3(0.4f, 0.8f, 1.2f) * k, mDark); // ammo box
                            P(vis, Shape.Cylinder, new Vector3(0f, 0f, 1.75f * k), new Vector3(90f, 0f, 0f), new Vector3(0.95f * k, 0.35f * k, 0.95f * k), mDark);
                            for (int n = 0; n < 6; n++)
                            {
                                float a = n * 60f * Mathf.Deg2Rad;
                                P(spin, Shape.Cylinder, new Vector3(Mathf.Cos(a) * 0.26f * k, Mathf.Sin(a) * 0.26f * k, 0.85f * k), new Vector3(90f, 0f, 0f), new Vector3(0.16f * k, 1.7f * k, 0.16f * k), mBlack);
                            }
                            P(spin, Shape.Cylinder, new Vector3(0f, 0f, 0.35f * k), new Vector3(90f, 0f, 0f), new Vector3(0.78f * k, 0.12f * k, 0.78f * k), mMetal);
                            P(spin, Shape.Cylinder, new Vector3(0f, 0f, 1.55f * k), new Vector3(90f, 0f, 0f), new Vector3(0.78f * k, 0.12f * k, 0.78f * k), mMetal);
                        }
                        spinners.Add(spin);
                        muzzles.Add(Group(b, "Muzzle", new Vector3(0f, 0f, 3.8f * k)));
                        break;
                    case WeaponId.RocketPods:
                        bool podModel = WeaponModel(b, "RocketPods", k, side);
                        if (!podModel)
                        {
                            Armor(vis, new Vector3(0f, 0f, 0.6f * k), Vector3.zero, new Vector3(1.4f, 1.4f, 2.0f) * k, mPrimary);
                            P(vis, Shape.Box, new Vector3(0f, 0.78f * k, 0.5f * k), Vector3.zero, new Vector3(1.0f, 0.12f, 1.6f) * k, mAccent);
                        }
                        for (int r = 0; r < 2; r++)
                            for (int col = 0; col < 2; col++)
                            {
                                Vector3 tp = new Vector3((col - 0.5f) * 0.6f * k, (r - 0.5f) * 0.6f * k, 1.62f * k);
                                if (!podModel)
                                {
                                    P(vis, Shape.Cylinder, tp, new Vector3(90f, 0f, 0f), new Vector3(0.42f * k, 0.1f * k, 0.42f * k), mBlack);
                                    P(vis, Shape.Sphere, tp - new Vector3(0f, 0f, 0.05f * k), Vector3.zero, new Vector3(0.3f, 0.3f, 0.1f) * k, mHot);
                                }
                                muzzles.Add(Group(b, "RocketMuzzle", tp + Vector3.forward * 0.25f * k));
                            }
                        break;
                    case WeaponId.Flamethrower:
                        if (!WeaponModel(b, "Flamethrower", k, side))
                        {
                            Armor(vis, new Vector3(0f, 0f, 0.5f * k), Vector3.zero, new Vector3(1.0f, 1.0f, 1.6f) * k, mSecondary);
                            P(vis, Shape.Cylinder, new Vector3(0f, 0f, 2.0f * k), new Vector3(90f, 0f, 0f), new Vector3(0.6f * k, 1.6f * k, 0.6f * k), mDark);
                            P(vis, Shape.Cylinder, new Vector3(0f, 0f, 2.9f * k), new Vector3(90f, 0f, 0f), new Vector3(0.95f * k, 0.4f * k, 0.95f * k), mMetal);
                            P(vis, Shape.Cylinder, new Vector3(0f, 0f, 3.12f * k), new Vector3(90f, 0f, 0f), new Vector3(0.6f * k, 0.06f * k, 0.6f * k), mHot);
                            P(vis, Shape.Cylinder, new Vector3(side * 0.62f * k, -0.1f * k, 0.6f * k), new Vector3(90f, 0f, 0f), new Vector3(0.62f * k, 1.4f * k, 0.62f * k), A.Paint(new Color(0.75f, 0.2f, 0.1f), 0.4f, 0.5f, false));
                            P(vis, Shape.Cylinder, new Vector3(0f, -0.48f * k, 1.6f * k), new Vector3(90f, 0f, 0f), new Vector3(0.18f * k, 1.6f * k, 0.18f * k), mBlack);
                        }
                        muzzles.Add(Group(b, "FlameMuzzle", new Vector3(0f, 0f, 3.25f * k)));
                        break;
                    case WeaponId.RailRifle:
                        if (!WeaponModel(b, "RailRifle", k, side))
                        {
                            Armor(vis, new Vector3(0f, 0f, 0.5f * k), Vector3.zero, new Vector3(1.0f, 1.2f, 2.4f) * k, mPrimary);
                            for (int s = -1; s <= 1; s += 2)
                                P(vis, Shape.Box, new Vector3(s * 0.32f * k, 0.1f * k, 3.8f * k), Vector3.zero, new Vector3(0.22f, 0.55f, 5.6f) * k, mDark);
                            for (int r = 0; r < 5; r++)
                                P(vis, Shape.Cylinder, new Vector3(0f, 0.1f * k, (1.9f + r * 0.95f) * k), new Vector3(90f, 0f, 0f), new Vector3(1.0f * k, 0.12f * k, 1.0f * k), mGlow);
                            P(vis, Shape.Cylinder, new Vector3(0f, 0.85f * k, 1.2f * k), new Vector3(90f, 0f, 0f), new Vector3(0.36f * k, 1.6f * k, 0.36f * k), mDark); // scope
                            P(vis, Shape.Box, new Vector3(0f, 0.85f * k, 2.02f * k), Vector3.zero, new Vector3(0.28f, 0.28f, 0.05f) * k, mGlow);
                        }
                        if (charge == null)
                        {
                            charge = Group(b, "ChargeGlow", new Vector3(0f, 0.1f * k, 6.4f * k));
                            P(charge, Shape.Sphere, Vector3.zero, Vector3.zero, Vector3.one, A.Glow(new Color(0.5f, 0.85f, 1f), 4f));
                            charge.localScale = Vector3.one * 0.2f;
                        }
                        muzzles.Add(Group(b, "Muzzle", new Vector3(0f, 0.1f * k, 6.8f * k)));
                        break;
                }
                MeshMerge.Merge(vis);
            }

            var turret = c.turret;
            turret.projectileSpeed = 0f;
            turret.compensateGravity = false;
            Weapon w;
            switch (id)
            {
                case WeaponId.PulseLaser:
                    var laser = c.root.AddComponent<PulseLaser>();
                    laser.cooldown = 0.12f;
                    laser.color = team == 0 ? new Color(0.3f, 0.85f, 1f) : new Color(1f, 0.25f, 0.2f);
                    w = laser;
                    break;
                case WeaponId.Gatling:
                    var gat = c.root.AddComponent<Gatling>();
                    gat.cooldown = 0.07f;
                    gat.spinners = spinners.ToArray();
                    gat.color = new Color(1f, 0.85f, 0.4f);
                    w = gat;
                    break;
                case WeaponId.RocketPods:
                    var pods = c.root.AddComponent<RocketPods>();
                    pods.cooldown = 0.26f;
                    pods.color = new Color(1f, 0.6f, 0.2f);
                    turret.projectileSpeed = 95f;
                    w = pods;
                    break;
                case WeaponId.Flamethrower:
                    var flame = c.root.AddComponent<Flamethrower>();
                    flame.cooldown = 0.1f;
                    flame.color = new Color(1f, 0.5f, 0.1f);
                    w = flame;
                    break;
                case WeaponId.RailRifle:
                    var rifle = c.root.AddComponent<PlasmaRailgun>();
                    rifle.cooldown = 2.2f;
                    rifle.chargeTime = 0.7f;
                    rifle.damage = 240f;
                    rifle.chargeGlow = charge;
                    rifle.color = new Color(0.6f, 0.9f, 1f);
                    w = rifle;
                    break;
                default:
                    var cannon = c.root.AddComponent<HeavyCannon>();
                    cannon.cooldown = player ? 0.75f : 1.1f;
                    cannon.color = new Color(1f, 0.6f, 0.2f);
                    turret.projectileSpeed = cannon.shellSpeed;
                    turret.compensateGravity = true;
                    w = cannon;
                    break;
            }
            w.displayName = Title(Loadout.Info(id).name);
            w.slot = Weapon.Slot.Primary;
            w.muzzles = muzzles.ToArray();
        }

        static void FitSpecial(Chassis c, WeaponId id, int team)
        {
            Weapon w;
            if (id == WeaponId.PlasmaRailgun)
            {
                // Shoulder cannon that aims with the arms.
                var m = c.shoulderMount != null ? c.shoulderMount : (c.armMounts.Length > 0 ? c.armMounts[0] : c.root.transform);
                float k = c.shoulderScale;
                var vis = Group(m, "RailStatic", Vector3.zero);
                if (!WeaponModel(m, "Railgun", k))
                {
                    Armor(vis, new Vector3(0f, 0f, 0.4f * k), Vector3.zero, new Vector3(1.3f, 1.1f, 2.0f) * k, mPrimary);
                    for (int s = -1; s <= 1; s += 2)
                        P(vis, Shape.Box, new Vector3(s * 0.36f * k, 0f, 3.2f * k), Vector3.zero, new Vector3(0.22f, 0.6f, 5.0f) * k, mDark);
                    for (int r = 0; r < 4; r++)
                        P(vis, Shape.Cylinder, new Vector3(0f, 0f, (1.6f + r * 1.05f) * k), new Vector3(90f, 0f, 0f), new Vector3(1.1f * k, 0.14f * k, 1.1f * k), mGlow);
                    P(vis, Shape.Box, new Vector3(0f, -0.62f * k, 0.6f * k), Vector3.zero, new Vector3(0.6f, 0.3f, 1.6f) * k, mDark);
                    MeshMerge.Merge(vis);
                }
                var glow = Group(m, "ChargeGlow", new Vector3(0f, 0f, 5.6f * k));
                P(glow, Shape.Sphere, Vector3.zero, Vector3.zero, Vector3.one, A.Glow(new Color(0.4f, 0.8f, 1f), 4f));
                glow.localScale = Vector3.one * 0.2f;
                var rail = c.root.AddComponent<PlasmaRailgun>();
                rail.cooldown = 7f;
                rail.chargeGlow = glow;
                rail.color = new Color(0.45f, 0.8f, 1f);
                rail.muzzles = new[] { Group(m, "RailMuzzle", new Vector3(0f, 0f, 5.9f * k)) };
                w = rail;
            }
            else
            {
                var mounts = c.backMounts.Length > 0 ? c.backMounts : new[] { c.root.transform };
                float k = c.backScale;
                var muzzles = new List<Transform>();
                for (int i = 0; i < mounts.Length; i++)
                {
                    var m = mounts[i];
                    float side = m.localPosition.x >= 0f ? 1f : -1f;
                    var vis = Group(m, "PackStatic", Vector3.zero);
                    switch (id)
                    {
                        case WeaponId.MortarBarrage:
                        {
                            int tubes = mounts.Length == 1 ? 3 : 2;
                            bool mm = WeaponModel(m, "Mortar" + tubes, k, side);
                            if (!mm) P(vis, Shape.Box, Vector3.zero, Vector3.zero, new Vector3(tubes * 0.85f + 0.4f, 0.5f, 1.9f) * k, mDark);
                            for (int n = 0; n < tubes; n++)
                            {
                                Vector3 tp = new Vector3((n - (tubes - 1) * 0.5f) * 0.85f * k, 0.6f * k, 0f);
                                if (!mm)
                                {
                                    P(vis, Shape.Cylinder, tp, new Vector3(-20f, 0f, 0f), new Vector3(0.62f * k, 1.0f * k, 0.62f * k), mSecondary);
                                    P(vis, Shape.Cylinder, tp + new Vector3(0f, 0.48f, 0.17f) * k, new Vector3(-20f, 0f, 0f), new Vector3(0.66f * k, 0.12f * k, 0.66f * k), mBlack);
                                    P(vis, Shape.Cylinder, tp + new Vector3(0f, 0.5f, 0.18f) * k, new Vector3(-20f, 0f, 0f), new Vector3(0.44f * k, 0.06f * k, 0.44f * k), mHot);
                                }
                                var mz = Group(m, "MortarMuzzle", tp + new Vector3(0f, 0.75f, 0.3f) * k);
                                mz.localRotation = Quaternion.Euler(-70f, 0f, 0f);
                                muzzles.Add(mz);
                            }
                            if (!mm) P(vis, Shape.Box, new Vector3(side * (tubes * 0.43f + 0.2f) * k, 0.3f * k, 0f), Vector3.zero, new Vector3(0.1f, 0.5f, 1.6f) * k, mAccent);
                            break;
                        }
                        case WeaponId.TeslaStorm:
                        {
                            if (i == 0)
                            {
                                if (!WeaponModel(m, "TeslaCoil", k)) {
                                P(vis, Shape.Cylinder, Vector3.zero, Vector3.zero, new Vector3(1.6f, 0.5f, 1.6f) * k, mDark);
                                for (int r = 0; r < 4; r++)
                                    P(vis, Shape.Cylinder, new Vector3(0f, (0.5f + r * 0.38f) * k, 0f), Vector3.zero, new Vector3((1.25f - r * 0.17f) * k, 0.13f * k, (1.25f - r * 0.17f) * k), r % 2 == 0 ? mMetal : A.Glow(new Color(0.5f, 0.7f, 1f), 2.5f));
                                P(vis, Shape.Cylinder, new Vector3(0f, 1.1f * k, 0f), Vector3.zero, new Vector3(0.32f * k, 1.4f * k, 0.32f * k), mDark);
                                P(vis, Shape.Sphere, new Vector3(0f, 2.2f * k, 0f), Vector3.zero, Vector3.one * 0.85f * k, A.Glow(new Color(0.55f, 0.75f, 1f), 4f));
                                }
                                muzzles.Insert(0, Group(m, "CoilTop", new Vector3(0f, 2.2f * k, 0f)));
                            }
                            else
                            {
                                // Capacitor bank feeding the coil
                                if (!WeaponModel(m, "TeslaBank", k, side)) {
                                Armor(vis, new Vector3(0f, 0.4f * k, 0f), new Vector3(-90f, 0f, 0f), new Vector3(1.5f, 1.6f, 0.8f) * k, mSecondary);
                                for (int r = 0; r < 3; r++)
                                    P(vis, Shape.Cylinder, new Vector3((r - 1) * 0.42f * k, 1.1f * k, 0f), Vector3.zero, new Vector3(0.3f * k, 0.5f * k, 0.3f * k), A.Glow(new Color(0.5f, 0.7f, 1f), 2f));
                                }
                            }
                            break;
                        }
                        default: // MissileSwarm
                        {
                            int cols = mounts.Length == 1 ? 4 : 3;
                            bool sm = WeaponModel(m, "Swarm" + cols, k, side);
                            if (!sm) Armor(vis, Vector3.zero, new Vector3(-90f, 0f, 0f), new Vector3(cols * 0.62f + 0.3f, 2.4f, 1.3f) * k, mSecondary);
                            if (!sm) P(vis, Shape.Box, new Vector3(side * (cols * 0.31f + 0.17f) * k, 0f, 0f), Vector3.zero, new Vector3(0.06f, 0.5f, 2.0f) * k, mAccent);
                            for (int r = 0; r < 2; r++)
                                for (int col = 0; col < cols; col++)
                                {
                                    Vector3 tp = new Vector3((col - (cols - 1) * 0.5f) * 0.62f * k, 0.7f * k, (0.5f - r * 0.95f) * k);
                                    if (!sm) P(vis, Shape.Cylinder, tp, Vector3.zero, new Vector3(0.46f * k, 0.08f * k, 0.46f * k), mBlack);
                                    if (!sm) P(vis, Shape.Sphere, tp + Vector3.up * 0.02f, Vector3.zero, new Vector3(0.3f, 0.1f, 0.3f) * k, A.Glow(new Color(1f, 0.25f, 0.1f), 2f));
                                    var tube = Group(m, "Tube", tp + Vector3.up * 0.1f);
                                    tube.localRotation = Quaternion.Euler(-70f, 0f, 0f);
                                    muzzles.Add(tube);
                                }
                            break;
                        }
                    }
                    MeshMerge.Merge(vis);
                }

                switch (id)
                {
                    case WeaponId.MortarBarrage:
                        var mortar = c.root.AddComponent<MortarBarrage>();
                        mortar.cooldown = 8f;
                        mortar.shells = 6;
                        w = mortar;
                        break;
                    case WeaponId.TeslaStorm:
                        var tesla = c.root.AddComponent<TeslaStorm>();
                        tesla.cooldown = 8f;
                        tesla.color = new Color(0.55f, 0.75f, 1f);
                        w = tesla;
                        break;
                    default:
                        var swarm = c.root.AddComponent<MissileSwarm>();
                        swarm.cooldown = 7f;
                        swarm.missilesPerSalvo = 10;
                        w = swarm;
                        break;
                }
                w.muzzles = muzzles.ToArray();
            }
            w.displayName = Title(Loadout.Info(id).name);
            w.slot = Weapon.Slot.Special;
        }

        /// <summary>"HEAVY CANNON" -> "Heavy Cannon"</summary>
        static string Title(string s)
        {
            var parts = s.ToLowerInvariant().Split(' ');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }
    }
}
