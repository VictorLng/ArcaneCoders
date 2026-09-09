using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcaneCode.Core
{
    public enum RoomKind { Entrance, Combat, Reward, Rest, Boss }
    [Serializable]
    public sealed class Room
    {
        public int X, Y, Template;
        public RoomKind Kind;
        public bool Visited, Cleared, Claimed;
        // XP belongs to the room until the player physically collects its crystal.
        public int UncollectedExperience;
        public readonly List<int> Neighbors = new List<int>();
    }

    public sealed class Dungeon
    {
        public int Seed;
        public readonly List<Room> Rooms = new List<Room>();
        public static Dungeon Generate(int seed)
        {
            var random = new Random(seed);
            // A connected tree avoids accidental adjacent doors and guarantees a leaf boss.
            for (int attempt = 0; attempt < 100; attempt++)
            {
                var dungeon = new Dungeon { Seed = seed };
                dungeon.Rooms.Add(new Room { Kind = RoomKind.Entrance, Cleared = true });
                int tries = 0;
                while (dungeon.Rooms.Count < 8 && tries++ < 400)
                {
                    int parent = random.Next(dungeon.Rooms.Count), dir = random.Next(4);
                    int x = dungeon.Rooms[parent].X + (dir == 0 ? 1 : dir == 1 ? -1 : 0);
                    int y = dungeon.Rooms[parent].Y + (dir == 2 ? 1 : dir == 3 ? -1 : 0);
                    if (dungeon.Rooms.Any(r => r.X == x && r.Y == y) || dungeon.Rooms.Count(r => Math.Abs(r.X - x) + Math.Abs(r.Y - y) == 1) != 1) continue;
                    int index = dungeon.Rooms.Count;
                    var room = new Room { X = x, Y = y, Kind = RoomKind.Combat, Template = random.Next(3) };
                    room.Neighbors.Add(parent); dungeon.Rooms[parent].Neighbors.Add(index); dungeon.Rooms.Add(room);
                }
                if (dungeon.Rooms.Count != 8 || !dungeon.Rooms.Any(r => r.Neighbors.Count >= 3)) continue;
                int[] distances = dungeon.Distances();
                int boss = Enumerable.Range(1, 7).OrderByDescending(i => distances[i]).First();
                dungeon.Rooms[boss].Kind = RoomKind.Boss;
                var candidates = Enumerable.Range(1, 7).Where(i => i != boss).OrderBy(i => random.Next()).ToArray();
                dungeon.Rooms[candidates[0]].Kind = RoomKind.Reward; dungeon.Rooms[candidates[0]].Cleared = true;
                dungeon.Rooms[candidates[1]].Kind = RoomKind.Rest; dungeon.Rooms[candidates[1]].Cleared = true;
                return dungeon;
            }
            // Deterministic guaranteed layout for the extremely unlikely exhausted generation.
            var fallback = new Dungeon { Seed = seed };
            int[,] cells = { {0,0}, {1,0}, {2,0}, {3,0}, {4,0}, {1,1}, {1,2}, {2,2} };
            for (int i = 0; i < 8; i++) fallback.Rooms.Add(new Room { X = cells[i,0], Y = cells[i,1], Kind = i == 0 ? RoomKind.Entrance : i == 4 ? RoomKind.Boss : i == 6 ? RoomKind.Rest : i == 7 ? RoomKind.Reward : RoomKind.Combat, Cleared = i == 0 || i >= 6 });
            for (int i = 0; i < 8; i++) for (int j = i + 1; j < 8; j++)
                if (Math.Abs(cells[i,0] - cells[j,0]) + Math.Abs(cells[i,1] - cells[j,1]) == 1)
                { fallback.Rooms[i].Neighbors.Add(j); fallback.Rooms[j].Neighbors.Add(i); }
            return fallback;
        }
        public int[] Distances()
        {
            int[] distance = Enumerable.Repeat(-1, Rooms.Count).ToArray(); distance[0] = 0;
            var queue = new Queue<int>(); queue.Enqueue(0);
            while (queue.Count > 0)
            { int current = queue.Dequeue(); foreach (int next in Rooms[current].Neighbors) if (distance[next] < 0) { distance[next] = distance[current] + 1; queue.Enqueue(next); } }
            return distance;
        }
    }

    [Serializable]
    public sealed class Profile
    {
        public int Version = 1, Coins, HealthRank, EnergyRank, BudgetRank;
        public bool FireUnlocked, IceUnlocked, SpeedCastUnlocked;
        public string FireCode = "", IceCode = "", FireDraft = "", IceDraft = "";
        public int Runs, Wins;
        public string LastBankedRun = "";
        public bool Bank(string runId, int coins, bool won)
        {
            if (string.IsNullOrEmpty(runId) || LastBankedRun == runId) return false;
            Coins += Math.Max(0, coins); Runs++; if (won) Wins++; LastBankedRun = runId; return true;
        }
    }
}
