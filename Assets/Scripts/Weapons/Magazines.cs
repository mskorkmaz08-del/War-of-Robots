using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Which guns carry a magazine and how long they take to reload. Rapid-fire and
    /// shell-firing guns can't fire forever: they empty, pause and reload on their own.
    /// Energy weapons (lasers, plasma, rail) keep firing on their own recharge.
    /// Per-shot damage of the magazine guns is raised in WeaponStats so their damage
    /// over a whole fire-and-reload cycle stays close to the energy weapons.
    /// </summary>
    public static class Magazines
    {
        public struct Mag
        {
            public int rounds;
            public float reload;
        }

        public static Mag For(WeaponId id)
        {
            switch (id)
            {
                case WeaponId.Gatling: return new Mag { rounds = 80, reload = 2.8f };      // ~5.6 s of fire
                case WeaponId.HeavyCannon: return new Mag { rounds = 6, reload = 3.2f };   // 6 shells
                case WeaponId.RocketPods: return new Mag { rounds = 12, reload = 3.2f };   // 6 pairs
                case WeaponId.Flamethrower: return new Mag { rounds = 50, reload = 3f };   // 5 s of fuel (0.1 s per round)
                case WeaponId.Autocannon: return new Mag { rounds = 5, reload = 3f };      // 5 three-shell bursts
                case WeaponId.ScatterCannon: return new Mag { rounds = 6, reload = 3.5f }; // 6 shells
                case WeaponId.GrenadeLauncher: return new Mag { rounds = 6, reload = 3.5f }; // one drum
                default: return new Mag();
            }
        }

        public static Mag For(Weapon w)
        {
            switch (w)
            {
                case Gatling _: return For(WeaponId.Gatling);
                case HeavyCannon _: return For(WeaponId.HeavyCannon);
                case RocketPods _: return For(WeaponId.RocketPods);
                case Flamethrower _: return For(WeaponId.Flamethrower);
                case Autocannon _: return For(WeaponId.Autocannon);
                case ScatterCannon _: return For(WeaponId.ScatterCannon);
                case GrenadeLauncher _: return For(WeaponId.GrenadeLauncher);
                default: return new Mag();
            }
        }

        /// <summary>Called by every weapon as it is created.</summary>
        public static void Configure(Weapon w)
        {
            var m = For(w);
            w.magazine = m.rounds;
            if (m.rounds > 0) w.reloadTime = m.reload;
        }

        /// <summary>Share of time a weapon is actually firing over a fire-and-reload cycle.</summary>
        public static float Sustain(WeaponId id, float cooldown)
        {
            var m = For(id);
            if (m.rounds <= 0) return 1f;
            float firing = m.rounds * cooldown;
            return firing / (firing + m.reload);
        }
    }
}
