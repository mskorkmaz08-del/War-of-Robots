using UnityEngine;

namespace RobotArena
{
    /// <summary>Where things go on a map: team bases, capture points, depots, reactor.</summary>
    public class MapLayout
    {
        /// <summary>Main base slots (first spawns, and respawns while the base is safe).</summary>
        public Vector3[] allySpawns;
        public Vector3[] enemySpawns;
        /// <summary>Reserve bases on the flanks for respawning when the main base is contested.</summary>
        public Vector3[] allyReserve = new Vector3[0];
        public Vector3[] enemyReserve = new Vector3[0];
        public Vector3[] Spawns(int team) => team == 0 ? allySpawns : enemySpawns;
        public Vector3[] Reserve(int team) => team == 0 ? allyReserve : enemyReserve;
        /// <summary>The centre of a team's main base.</summary>
        public Vector3 Base(int team) => Spawns(team)[0];
        public Vector3[] zones;
        public float zoneRadius = 20f;
        public Vector3[] depots;
        public Vector3 reactor;
    
        public static MapLayout For(MapId map)
        {
            var l = new MapLayout
            {
                allySpawns = new[] { new Vector3(0f, 0f, -105f), new Vector3(-40f, 0f, -105f), new Vector3(40f, 0f, -105f), new Vector3(-68f, 0f, -112f), new Vector3(68f, 0f, -112f) },
                enemySpawns = new[] { new Vector3(0f, 0f, 100f), new Vector3(-40f, 0f, 100f), new Vector3(40f, 0f, 100f), new Vector3(-68f, 0f, 110f), new Vector3(68f, 0f, 110f) },
            };
            switch (map)
            {
                case MapId.Foundry:
                    l.zones = new[] { new Vector3(-75f, 0f, -35f), new Vector3(0f, 0f, 0f), new Vector3(78f, 0f, 15f) };
                    l.zoneRadius = 22f;
                    l.depots = new[] { new Vector3(-92f, 0f, 42f), new Vector3(0f, 0f, 82f), new Vector3(86f, 0f, 52f) };
                    l.reactor = new Vector3(0f, 0f, -80f);
                    break;
                case MapId.Dockyard:
                    l.zones = new[] { new Vector3(-80f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(80f, 0f, 0f) };
                    l.depots = new[] { new Vector3(-90f, 0f, 64f), new Vector3(0f, 0f, 82f), new Vector3(90f, 0f, 60f) };
                    l.reactor = new Vector3(0f, 0f, -82f);
                    break;
                default:
                    l.zones = new[] { new Vector3(-80f, 0f, 5f), new Vector3(0f, 0f, 0f), new Vector3(80f, 0f, -5f) };
                    l.depots = new[] { new Vector3(-84f, 0f, 56f), new Vector3(0f, 0f, 80f), new Vector3(84f, 0f, 56f) };
                    l.reactor = new Vector3(0f, 0f, -78f);
                    break;
            }
            // The battlefield is now over a kilometre across. Each team's main base sits
            // deep in its own end, 860 m from the other; two reserve bases on the flanks
            // take over when the enemy pushes into the main one. Objectives spread across
            // the middle so the fighting happens between the bases, not on them.
            float bz = Frontier.Half - 80f;
            l.allySpawns = BaseSlots(new Vector3(0f, 0f, -bz), 1f);
            l.enemySpawns = BaseSlots(new Vector3(0f, 0f, bz), -1f);
            l.allyReserve = new[] { new Vector3(-330f, 0f, -bz + 40f), new Vector3(330f, 0f, -bz + 40f), new Vector3(-360f, 0f, -bz + 70f), new Vector3(360f, 0f, -bz + 70f) };
            l.enemyReserve = new[] { new Vector3(330f, 0f, bz - 40f), new Vector3(-330f, 0f, bz - 40f), new Vector3(360f, 0f, bz - 70f), new Vector3(-360f, 0f, bz - 70f) };
            for (int i = 0; i < l.zones.Length; i++) l.zones[i] *= 2.4f;
            for (int i = 0; i < l.depots.Length; i++) l.depots[i] = new Vector3(l.depots[i].x * 2.4f, 0f, 150f + l.depots[i].z * 1.2f);
            l.reactor = new Vector3(0f, 0f, -200f);
            l.zoneRadius *= 1.4f;
            return l;
        }

        /// <summary>Five slots of a base, in a shallow arc facing the enemy (dir +1 faces +z).</summary>
        static Vector3[] BaseSlots(Vector3 c, float dir)
        {
            return new[]
            {
                c, c + new Vector3(-34f, 0f, -6f * dir), c + new Vector3(34f, 0f, -6f * dir),
                c + new Vector3(-68f, 0f, -16f * dir), c + new Vector3(68f, 0f, -16f * dir)
            };
        }

    }
}
