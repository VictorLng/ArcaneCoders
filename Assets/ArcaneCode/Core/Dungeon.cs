using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcaneCode.Core
{
    public enum RoomKind { Entrance, Combat, Shop, Rest, Treasure, Boss }
    [Serializable]
    public sealed class Room
    {
        public int X, Y, Template, Floor;
        public RoomKind Kind;
        public bool Visited, Cleared, Claimed;
        public bool ChestOpened;
        public readonly List<RunItemInstance> UncollectedItems = new List<RunItemInstance>();
        // XP belongs to the room until the player physically collects its crystal.
        public int UncollectedExperience;
        // Coins belong to the room until the player physically collects them.
        public int UncollectedCoins;
        public readonly List<int> Neighbors = new List<int>();
    }

    public sealed class Dungeon
    {
        public int Seed;
        // The first two floors alternate between these two families for the whole run.
        public bool UndeadFirst;
        public readonly List<Room> Rooms = new List<Room>();
        public static Dungeon Generate(int seed)
        {
            var random = new Random(seed);

            for (int attempt = 0; attempt < 100; attempt++)
            {
                var dungeon = new Dungeon { Seed = seed, UndeadFirst = random.Next(2) == 0 };
                dungeon.Rooms.Add(new Room { Kind = RoomKind.Entrance, Cleared = true });
                int tries = 0;
                while (dungeon.Rooms.Count < 9 && tries++ < 500)
                {
                    int parent = random.Next(dungeon.Rooms.Count), dir = random.Next(4);
                    int x = dungeon.Rooms[parent].X + (dir == 0 ? 1 : dir == 1 ? -1 : 0);
                    int y = dungeon.Rooms[parent].Y + (dir == 2 ? 1 : dir == 3 ? -1 : 0);
                    if (dungeon.Rooms.Any(r => r.X == x && r.Y == y) || dungeon.Rooms.Count(r => Math.Abs(r.X - x) + Math.Abs(r.Y - y) == 1) != 1) continue;
                    int index = dungeon.Rooms.Count;
                    var room = new Room { X = x, Y = y, Kind = RoomKind.Combat, Template = random.Next(3) };
                    room.Neighbors.Add(parent); dungeon.Rooms[parent].Neighbors.Add(index); dungeon.Rooms.Add(room);
                }
                if (dungeon.Rooms.Count != 9 || !dungeon.Rooms.Any(r => r.Neighbors.Count >= 3)) continue;
                int[] distances = dungeon.Distances();
                int boss = Enumerable.Range(1, 8).OrderByDescending(i => distances[i]).First();
                dungeon.Rooms[boss].Kind = RoomKind.Boss;
                var candidates = Enumerable.Range(1, 8).Where(i => i != boss).OrderBy(i => random.Next()).ToArray();
                dungeon.Rooms[candidates[0]].Kind = RoomKind.Shop; dungeon.Rooms[candidates[0]].Cleared = true;
                dungeon.Rooms[candidates[1]].Kind = RoomKind.Rest; dungeon.Rooms[candidates[1]].Cleared = true;
                dungeon.Rooms[candidates[2]].Kind = RoomKind.Treasure; dungeon.Rooms[candidates[2]].Cleared = true;
                AssignFloors(dungeon, boss);
                return dungeon;
            }
            // Deterministic guaranteed layout for the extremely unlikely exhausted generation.
            var fallback = new Dungeon { Seed = seed, UndeadFirst = new Random(seed).Next(2) == 0 };
            int[,] cells = { {0,0}, {1,0}, {2,0}, {3,0}, {4,0}, {1,1}, {1,2}, {2,2}, {3,2} };
            for (int i = 0; i < 9; i++) fallback.Rooms.Add(new Room { X = cells[i,0], Y = cells[i,1], Kind = i == 0 ? RoomKind.Entrance : i == 4 ? RoomKind.Boss : i == 6 ? RoomKind.Rest : i == 7 ? RoomKind.Shop : i == 8 ? RoomKind.Treasure : RoomKind.Combat, Cleared = i == 0 || i >= 6 });
            for (int i = 0; i < 9; i++) for (int j = i + 1; j < 9; j++)
                if (Math.Abs(cells[i,0] - cells[j,0]) + Math.Abs(cells[i,1] - cells[j,1]) == 1)
                { fallback.Rooms[i].Neighbors.Add(j); fallback.Rooms[j].Neighbors.Add(i); }
            AssignFloors(fallback, 4);
            return fallback;
        }
        static void AssignFloors(Dungeon dungeon, int boss)
        {
            int[] distances = dungeon.Distances();
            int[] combat = Enumerable.Range(0,dungeon.Rooms.Count)
                .Where(i => dungeon.Rooms[i].Kind == RoomKind.Combat)
                .OrderBy(i => distances[i]).ThenBy(i => i).ToArray();
            for (int i=0;i<combat.Length;i++) dungeon.Rooms[combat[i]].Floor = i + 1;
            dungeon.Rooms[boss].Floor = 5;
            for (int i=0;i<dungeon.Rooms.Count;i++)
                if (i != boss && dungeon.Rooms[i].Kind != RoomKind.Combat)
                    dungeon.Rooms[i].Floor = i == 0 ? 0 : Math.Max(1,Math.Min(4,distances[i]));
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
        public const int CurrentVersion = 6;
        public int Version = CurrentVersion, Coins, HealthRank, EnergyRank, BudgetRank;
        public bool FireUnlocked, IceUnlocked, SpeedCastUnlocked, TutorialsEnabled = true;
        public string MageStartingGrimoireId = "grimoire-fire";
        // Legacy per-class sources remain so version 1 profiles can be migrated safely.
        public string FireCode = "", IceCode = "", FireDraft = "", IceDraft = "";
        // Retained only for migrations from profiles preceding character-owned programming.
        public string MageCode = "", MageDraft = "";
        public StaffInstance EquippedStaff = new StaffInstance();
        public GrimoireInstance EquippedGrimoire;
        public List<SavedGrimoire> Library = new List<SavedGrimoire>();
        public List<CharacterProgramState> CharacterPrograms = new List<CharacterProgramState>();
        public List<SavedCharacterProgram> ProgramLibrary = new List<SavedCharacterProgram>();
        public int Runs, Wins;
        public string LastBankedRun = "";
        public CharacterProgramState ProgramFor(string classId)
        {
            if (string.IsNullOrWhiteSpace(classId)) throw new ArgumentException("Classe do personagem não informada.",nameof(classId));
            if (CharacterPrograms == null) CharacterPrograms = new List<CharacterProgramState>();
            CharacterProgramState program = CharacterPrograms.Find(entry => entry != null && entry.ClassId == classId);
            if (program != null) return program;
            program = new CharacterProgramState { ClassId=classId };
            CharacterPrograms.Add(program);
            return program;
        }
        public bool Bank(string runId, int coins, bool won)
        {
            if (string.IsNullOrEmpty(runId) || LastBankedRun == runId) return false;
            Coins += Math.Max(0, coins); Runs++; if (won) Wins++; LastBankedRun = runId; return true;
        }
    }
}
