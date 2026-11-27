using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>Every weapon that can be fitted to a robot.</summary>
    public enum WeaponId
    {
        // Primary (arm) weapons
        HeavyCannon, PulseLaser, Gatling, RocketPods, Flamethrower, RailRifle,
        // Special (shoulder and back) weapons
        MissileSwarm, PlasmaRailgun, MortarBarrage, TeslaStorm,
        // Added later: kept at the end so saved loadouts keep their numbers
        Autocannon, ScatterCannon, IonBeam, GrenadeLauncher,
        ClusterBomb, EmpBlast, Hellfire
    }

    public class WeaponInfo
    {
        public WeaponId id;
        public string name;
        public string blurb;
        public bool special;
        // 0..1 for the hangar's stat bars
        public float damage, rate, reach;
    }

    /// <summary>
    /// Which weapons a robot carries. The hangar lets the player swap the primary
    /// (arm) weapon and the special (shoulder/back) weapon on every robot; the
    /// choice is remembered per robot.
    /// </summary>
    public struct Loadout
    {
        public WeaponId primary;
        public WeaponId special;

        public Loadout(WeaponId primary, WeaponId special)
        {
            this.primary = primary;
            this.special = special;
        }

        public static readonly List<WeaponInfo> Weapons = new List<WeaponInfo>
        {
            new WeaponInfo { id = WeaponId.HeavyCannon, name = "HEAVY CANNON", blurb = "Explosive shells. Slow, hits hard, splash damage.", damage = 0.85f, rate = 0.35f, reach = 0.75f },
            new WeaponInfo { id = WeaponId.PulseLaser, name = "PULSE LASER", blurb = "Instant-hit energy bolts at a very high rate.", damage = 0.35f, rate = 0.95f, reach = 0.85f },
            new WeaponInfo { id = WeaponId.Gatling, name = "GATLING", blurb = "Spins up, then shreds armour with a stream of rounds.", damage = 0.3f, rate = 1f, reach = 0.7f },
            new WeaponInfo { id = WeaponId.RocketPods, name = "ROCKET PODS", blurb = "Fast unguided rockets with small blasts.", damage = 0.6f, rate = 0.7f, reach = 0.6f },
            new WeaponInfo { id = WeaponId.Flamethrower, name = "FLAMETHROWER", blurb = "Short range, burns everything in a wide cone.", damage = 1f, rate = 1f, reach = 0.2f },
            new WeaponInfo { id = WeaponId.RailRifle, name = "RAIL RIFLE", blurb = "Charged sniper shot. Huge damage at any range.", damage = 1f, rate = 0.2f, reach = 1f },
            new WeaponInfo { id = WeaponId.Autocannon, name = "AUTOCANNON", blurb = "40 mm bursts of three explosive shells. Accurate at range.", damage = 0.55f, rate = 0.75f, reach = 0.8f },
            new WeaponInfo { id = WeaponId.ScatterCannon, name = "SCATTER CANNON", blurb = "Nine heavy slugs per blast. Devastating up close, weak far off.", damage = 0.95f, rate = 0.45f, reach = 0.3f },
            new WeaponInfo { id = WeaponId.IonBeam, name = "ION BEAM", blurb = "A steady beam that burns hotter the longer it stays on target.", damage = 0.6f, rate = 1f, reach = 0.65f },
            new WeaponInfo { id = WeaponId.GrenadeLauncher, name = "GRENADE LAUNCHER", blurb = "Revolver launcher lobbing heavy grenades over low cover.", damage = 0.75f, rate = 0.55f, reach = 0.6f },
            new WeaponInfo { id = WeaponId.MissileSwarm, name = "MISSILE SWARM", blurb = "A salvo of homing missiles from shoulder pods.", special = true, damage = 0.9f, rate = 0.4f, reach = 0.8f },
            new WeaponInfo { id = WeaponId.PlasmaRailgun, name = "PLASMA RAILGUN", blurb = "Charges, then fires a blast that knocks robots back.", special = true, damage = 1f, rate = 0.3f, reach = 1f },
            new WeaponInfo { id = WeaponId.MortarBarrage, name = "MORTAR BARRAGE", blurb = "Lobs a rain of shells over cover onto the target.", special = true, damage = 0.85f, rate = 0.3f, reach = 0.85f },
            new WeaponInfo { id = WeaponId.TeslaStorm, name = "TESLA STORM", blurb = "Lightning that chains between up to 3 enemies.", special = true, damage = 0.75f, rate = 0.35f, reach = 0.6f },
            new WeaponInfo { id = WeaponId.ClusterBomb, name = "CLUSTER BOMB", blurb = "A canister that bursts over the target into twelve bomblets.", special = true, damage = 0.95f, rate = 0.3f, reach = 0.75f },
            new WeaponInfo { id = WeaponId.EmpBlast, name = "EMP BLAST", blurb = "A shockwave that hurts and knocks out enemy weapons nearby.", special = true, damage = 0.5f, rate = 0.25f, reach = 0.3f },
            new WeaponInfo { id = WeaponId.Hellfire, name = "HELLFIRE", blurb = "Two heavy rockets that climb, then dive onto the target.", special = true, damage = 1f, rate = 0.3f, reach = 0.95f },
        };

        public static WeaponInfo Info(WeaponId id)
        {
            foreach (var w in Weapons) if (w.id == id) return w;
            return Weapons[0];
        }

        public static List<WeaponId> Choices(bool special)
        {
            var list = new List<WeaponId>();
            foreach (var w in Weapons) if (w.special == special) list.Add(w.id);
            return list;
        }

        public static Loadout Default(RobotType type)
        {
            switch (type)
            {
                case RobotType.Raptor: return new Loadout(WeaponId.PulseLaser, WeaponId.PlasmaRailgun);
                case RobotType.Arachne: return new Loadout(WeaponId.Gatling, WeaponId.MortarBarrage);
                case RobotType.Hornet: return new Loadout(WeaponId.RocketPods, WeaponId.TeslaStorm);
                case RobotType.Juggernaut: return new Loadout(WeaponId.Flamethrower, WeaponId.MissileSwarm);
                case RobotType.Lancer: return new Loadout(WeaponId.RailRifle, WeaponId.MissileSwarm);
                case RobotType.Bastion: return new Loadout(WeaponId.HeavyCannon, WeaponId.MortarBarrage);
                case RobotType.Vanguard: return new Loadout(WeaponId.PulseLaser, WeaponId.MissileSwarm);
                case RobotType.Mammoth: return new Loadout(WeaponId.Gatling, WeaponId.PlasmaRailgun);
                default: return new Loadout(WeaponId.HeavyCannon, WeaponId.MissileSwarm);
            }
        }

        /// <summary>A random fit for an AI robot: one of the arm weapons and one of the specials.</summary>
        public static Loadout RandomFor(RobotType type)
        {
            var p = Choices(false);
            var s = Choices(true);
            var d = Default(type);
            return new Loadout(Random.value < 0.7f ? p[Random.Range(0, p.Count)] : d.primary, Random.value < 0.7f ? s[Random.Range(0, s.Count)] : d.special);
        }

        /// <summary>The player's saved loadout for a robot (or its default).</summary>
        public static Loadout ForPlayer(RobotType type)
        {
            var d = Default(type);
            string key = "loadout_" + type;
            if (!PlayerPrefs.HasKey(key)) return d;
            return Unpack(PlayerPrefs.GetInt(key), d);
        }

        public static void SavePlayer(RobotType type, Loadout l)
        {
            PlayerPrefs.SetInt("loadout_" + type, l.Pack());
            PlayerPrefs.Save();
        }

        /// <summary>Two weapons in one small number (for saving and for online play).</summary>
        public int Pack() => (int)primary | ((int)special << 8);

        public static Loadout Unpack(int packed, Loadout fallback)
        {
            var p = (WeaponId)(packed & 0xff);
            var s = (WeaponId)((packed >> 8) & 0xff);
            bool okP = System.Enum.IsDefined(typeof(WeaponId), p) && !Info(p).special;
            bool okS = System.Enum.IsDefined(typeof(WeaponId), s) && Info(s).special;
            return new Loadout(okP ? p : fallback.primary, okS ? s : fallback.special);
        }
    }
}
