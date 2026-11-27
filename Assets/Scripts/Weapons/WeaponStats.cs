using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// One table for how hard every weapon hits and how fast it reloads, used for
    /// every robot on both teams. Main weapons are tuned to roughly the same damage
    /// per second (about 150) and differ in how they deliver it; specials are big
    /// bursts on a long reload. Upgrades raise damage 10% and cut reload 3% per level.
    /// </summary>
    public static class WeaponStats
    {
        public const int MaxLevel = 5;

        public struct Stats
        {
            public float damage;     // per hit (per missile, per shell, per second for the flamethrower)
            public float cooldown;   // seconds between shots or salvos
            public int count;        // missiles or shells per salvo (specials), 0 = n/a
        }

        public static Stats Base(WeaponId id)
        {
            switch (id)
            {
                case WeaponId.HeavyCannon: return new Stats { damage = 120f, cooldown = 0.8f };
                case WeaponId.PulseLaser: return new Stats { damage = 18f, cooldown = 0.12f };
                case WeaponId.Gatling: return new Stats { damage = 11f, cooldown = 0.07f };
                case WeaponId.RocketPods: return new Stats { damage = 42f, cooldown = 0.28f };
                case WeaponId.Flamethrower: return new Stats { damage = 190f, cooldown = 0.1f };
                case WeaponId.RailRifle: return new Stats { damage = 300f, cooldown = 2.2f };
                case WeaponId.MissileSwarm: return new Stats { damage = 45f, cooldown = 8f, count = 10 };
                case WeaponId.PlasmaRailgun: return new Stats { damage = 420f, cooldown = 7f };
                case WeaponId.MortarBarrage: return new Stats { damage = 80f, cooldown = 8f, count = 6 };
                default: return new Stats { damage = 160f, cooldown = 8f }; // Tesla Storm, per target
            }
        }

        public static float DamageMultiplier(int level) => 1f + 0.1f * Mathf.Clamp(level, 0, MaxLevel);
        public static float CooldownMultiplier(int level) => 1f - 0.03f * Mathf.Clamp(level, 0, MaxLevel);

        public static Stats At(WeaponId id, int level)
        {
            var s = Base(id);
            s.damage *= DamageMultiplier(level);
            s.cooldown *= CooldownMultiplier(level);
            return s;
        }

        /// <summary>Damage per second for the hangar (bursts spread over their reload).</summary>
        public static float Dps(WeaponId id, int level)
        {
            var s = At(id, level);
            switch (id)
            {
                case WeaponId.Flamethrower: return s.damage;
                case WeaponId.RailRifle: return s.damage / (s.cooldown + 0.7f);
                case WeaponId.PlasmaRailgun: return s.damage / (s.cooldown + 1.3f);
                case WeaponId.MissileSwarm:
                case WeaponId.MortarBarrage: return s.damage * s.count / s.cooldown;
                default: return s.damage / s.cooldown;
            }
        }

        /// <summary>Damage of one shot or salvo, for the hangar.</summary>
        public static float Hit(WeaponId id, int level)
        {
            var s = At(id, level);
            return s.count > 0 ? s.damage * s.count : s.damage;
        }

        /// <summary>Sets a fitted weapon's damage and reload from the table.</summary>
        public static void Apply(Weapon w, WeaponId id, int level)
        {
            if (w == null) return;
            var s = At(id, level);
            w.cooldown = s.cooldown;
            switch (w)
            {
                case HeavyCannon c: c.damage = s.damage; break;
                case PulseLaser l: l.damage = s.damage; break;
                case Gatling g: g.damage = s.damage; break;
                case RocketPods r: r.damage = s.damage; break;
                case Flamethrower f: f.damagePerSecond = s.damage; break;
                case PlasmaRailgun p: p.damage = s.damage; break;
                case MissileSwarm m: m.damagePerMissile = s.damage; if (s.count > 0) m.missilesPerSalvo = s.count; break;
                case MortarBarrage b: b.damage = s.damage; if (s.count > 0) b.shells = s.count; break;
                case TeslaStorm t: t.damage = s.damage; break;
            }
        }
    }

    /// <summary>Armour of each machine: the bigger the robot, the more it can take.</summary>
    public static class Balance
    {
        public static float Health(RobotType type)
        {
            switch (type)
            {
                case RobotType.Colossus: return 4600f;   // the super-heavy
                case RobotType.Titan: return 4000f;
                case RobotType.Mammoth: return 3700f;
                case RobotType.Juggernaut: return 3400f;
                case RobotType.Scarab: return 3200f;
                case RobotType.Bastion: return 3100f;
                case RobotType.Arachne: return 2700f;
                case RobotType.Vanguard: return 2600f;
                case RobotType.Raptor: return 2300f;
                case RobotType.Lancer: return 2100f;
                case RobotType.Strider: return 1900f;
                default: return 1800f;                   // Hornet, the smallest
            }
        }

        /// <summary>Seconds a robot can't be hurt after it (re)spawns, unless it opens fire.</summary>
        public const float SpawnShield = 5f;
    }

    /// <summary>
    /// Credits earned in battle and the weapon upgrade levels they buy, saved on the
    /// device. Upgrades belong to the weapon, so a Heavy Cannon upgrade helps every
    /// robot that carries one.
    /// </summary>
    public static class Upgrades
    {
        const string CreditsKey = "wor_credits";

        public static int Credits
        {
            get => PlayerPrefs.GetInt(CreditsKey, 0);
            set { PlayerPrefs.SetInt(CreditsKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static int Level(WeaponId id) => Mathf.Clamp(PlayerPrefs.GetInt("wup_" + id, 0), 0, WeaponStats.MaxLevel);

        static readonly int[] costs = { 300, 600, 1000, 1500, 2200 };

        /// <summary>Price of the next level, or -1 at max level.</summary>
        public static int NextCost(WeaponId id)
        {
            int l = Level(id);
            return l >= WeaponStats.MaxLevel ? -1 : costs[l];
        }

        public static bool TryBuy(WeaponId id)
        {
            int cost = NextCost(id);
            if (cost < 0 || Credits < cost) return false;
            Credits -= cost;
            PlayerPrefs.SetInt("wup_" + id, Level(id) + 1);
            PlayerPrefs.Save();
            return true;
        }

        public const int PerKill = 150;
        public const int PerWin = 500;
        public const int PerLoss = 150;
    }
}
