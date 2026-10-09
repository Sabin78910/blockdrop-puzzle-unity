using System;
using System.Collections.Generic;
using System.Text;

namespace BlockDrop.Core
{
    /// <summary>Deals a hand of three pieces; a new hand arrives once all three are used.</summary>
    public sealed class Dealer
    {
        public const int HandSize = 3;
        private readonly PieceGenerator _gen;
        public readonly Piece[] Hand = new Piece[HandSize];

        public Dealer(int seed)
        {
            _gen = new PieceGenerator(seed);
            Deal();
        }

        private void Deal()
        {
            for (int i = 0; i < HandSize; i++) Hand[i] = _gen.Next();
        }

        public void Use(int index)
        {
            Hand[index] = null;
            foreach (var p in Hand) if (p != null) return;
            Deal();
        }
    }

    /// <summary>One player's board, hand and move count.</summary>
    public sealed class Seat
    {
        public readonly Board Board;
        public readonly Dealer Dealer;
        public int Moves { get; private set; }
        public bool Out { get; private set; }

        public Seat(int seed, int size = 8)
        {
            Board = new Board(size);
            Dealer = new Dealer(seed);
        }

        public bool TryPlace(int handIndex, int x, int y)
        {
            var piece = Dealer.Hand[handIndex];
            if (Out || piece == null || !Board.CanPlace(piece, x, y)) return false;
            Board.Place(piece, x, y);
            Dealer.Use(handIndex);
            Moves++;
            Out = Board.IsGameOver(Dealer.Hand);
            return true;
        }
    }

    /// <summary>Greedy player: used to calibrate level targets (every level is beatable) and for hints.</summary>
    public static class Bot
    {
        public static bool TryBestMove(Board board, Piece[] hand, out int handIndex, out int x, out int y)
        {
            handIndex = x = y = -1;
            int best = int.MinValue;
            for (int i = 0; i < hand.Length; i++)
            {
                var p = hand[i];
                if (p == null) continue;
                for (int px = 0; px < board.Size; px++)
                    for (int py = 0; py < board.Size; py++)
                    {
                        if (!board.CanPlace(p, px, py)) continue;
                        var sim = board.Clone();
                        int score = sim.Place(p, px, py).Points * 100 - TrappedCells(sim) * 8 + Contacts(board, p, px, py);
                        if (score > best) { best = score; handIndex = i; x = px; y = py; }
                    }
            }
            return handIndex >= 0;
        }

        private static int TrappedCells(Board b)
        {
            int n = 0;
            for (int x = 0; x < b.Size; x++)
                for (int y = 0; y < b.Size; y++)
                    if (!b.IsFilled(x, y) && Blocked(b, x - 1, y) && Blocked(b, x + 1, y) && Blocked(b, x, y - 1) && Blocked(b, x, y + 1)) n++;
            return n;
        }

        private static bool Blocked(Board b, int x, int y) => x < 0 || y < 0 || x >= b.Size || y >= b.Size || b.IsFilled(x, y);

        private static int Contacts(Board b, Piece p, int ox, int oy)
        {
            int n = 0;
            foreach (var (cx, cy) in p.Cells)
            {
                int x = ox + cx, y = oy + cy;
                if (Blocked(b, x - 1, y)) n++;
                if (Blocked(b, x + 1, y)) n++;
                if (Blocked(b, x, y - 1)) n++;
                if (Blocked(b, x, y + 1)) n++;
            }
            return n;
        }

        /// <summary>Plays a seat greedily until moves run out or it is stuck; returns final score.</summary>
        public static int Play(Seat seat, int maxMoves)
        {
            while (!seat.Out && seat.Moves < maxMoves && TryBestMove(seat.Board, seat.Dealer.Hand, out int i, out int x, out int y))
                seat.TryPlace(i, x, y);
            return seat.Board.Score;
        }
    }

    public sealed class LevelDefinition
    {
        public int Number, Seed, MaxMoves, TargetScore, TwoStarScore, ThreeStarScore;
        public (int x, int y)[] Obstacles;

        public int StarsFor(int score) =>
            score >= ThreeStarScore ? 3 : score >= TwoStarScore ? 2 : score >= TargetScore ? 1 : 0;
    }

    /// <summary>30 deterministic levels. Difficulty rises with more obstacles, fewer spare moves and
    /// higher targets. Targets are calibrated against the greedy bot, so every level is winnable.</summary>
    public static class LevelLibrary
    {
        public const int Count = 30;
        public const int ObstacleColor = 5;
        private static readonly Dictionary<int, LevelDefinition> Cache = new Dictionary<int, LevelDefinition>();

        public static LevelDefinition Get(int number)
        {
            if (number < 1 || number > Count) throw new ArgumentOutOfRangeException(nameof(number));
            if (Cache.TryGetValue(number, out var cached)) return cached;

            var def = new LevelDefinition
            {
                Number = number,
                Seed = 1000 + number * 7919,
                MaxMoves = 20 + number / 2,
                Obstacles = MakeObstacles(number),
            };
            int bot = Bot.Play(NewSeat(def), def.MaxMoves);
            double difficulty = 0.55 + 0.35 * (number - 1) / (Count - 1);
            def.TargetScore = Math.Max(1, Math.Min(bot, (int)(bot * difficulty)));
            def.TwoStarScore = Math.Max(def.TargetScore, (def.TargetScore + bot) / 2);
            def.ThreeStarScore = Math.Max(def.TwoStarScore, bot);
            Cache[number] = def;
            return def;
        }

        public static Seat NewSeat(LevelDefinition def)
        {
            var seat = new Seat(def.Seed);
            foreach (var (x, y) in def.Obstacles) seat.Board.SetCell(x, y, ObstacleColor);
            return seat;
        }

        private static (int, int)[] MakeObstacles(int number)
        {
            var rng = new Random(number * 104729);
            int count = Math.Min(2 + number * 2 / 3, 22);
            var set = new HashSet<(int, int)>();
            var rows = new int[8];
            var cols = new int[8];
            while (set.Count < count)
            {
                int x = rng.Next(8), y = rng.Next(8);
                // keep lines open: never more than 5 obstacles in any row or column
                if (set.Contains((x, y)) || rows[y] >= 5 || cols[x] >= 5) continue;
                set.Add((x, y)); rows[y]++; cols[x]++;
            }
            var list = new List<(int, int)>(set);
            list.Sort();
            return list.ToArray();
        }
    }

    public enum LevelState { Playing, Won, Lost }

    public sealed class LevelSession
    {
        public readonly LevelDefinition Definition;
        public readonly Seat Seat;

        public LevelSession(int number)
        {
            Definition = LevelLibrary.Get(number);
            Seat = LevelLibrary.NewSeat(Definition);
        }

        public int MovesLeft => Math.Max(0, Definition.MaxMoves - Seat.Moves);

        /// <summary>Won once the target is reached and moves run out (or the board locks up);
        /// lost if the target was not reached by then.</summary>
        public LevelState State
        {
            get
            {
                if (MovesLeft > 0 && !Seat.Out) return LevelState.Playing;
                return Seat.Board.Score >= Definition.TargetScore ? LevelState.Won : LevelState.Lost;
            }
        }

        public int Stars => State == LevelState.Won ? Definition.StarsFor(Seat.Board.Score) : 0;

        public bool TryPlace(int handIndex, int x, int y) => State == LevelState.Playing && Seat.TryPlace(handIndex, x, y);
    }

    /// <summary>Which levels are unlocked and their best stars. Serialized to a short string for PlayerPrefs.</summary>
    public sealed class Progress
    {
        private readonly int[] _stars = new int[LevelLibrary.Count + 1];

        public int StarsFor(int level) => _stars[level];
        public bool IsUnlocked(int level) => level == 1 || (level <= LevelLibrary.Count && _stars[level - 1] > 0);
        public int TotalStars { get { int t = 0; foreach (var s in _stars) t += s; return t; } }

        public void Record(int level, int stars)
        {
            if (stars > _stars[level]) _stars[level] = stars;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            for (int i = 1; i <= LevelLibrary.Count; i++) sb.Append(_stars[i]);
            return sb.ToString();
        }

        public static Progress Parse(string data)
        {
            var p = new Progress();
            if (string.IsNullOrEmpty(data)) return p;
            for (int i = 0; i < data.Length && i < LevelLibrary.Count; i++)
                if (data[i] >= '0' && data[i] <= '3') p._stars[i + 1] = data[i] - '0';
            return p;
        }
    }

    /// <summary>Local pass-and-play for two players. Both get the identical piece sequence (same seed),
    /// take turns placing one piece, and drop out when stuck. Highest score wins.</summary>
    public sealed class VersusMatch
    {
        public readonly Seat[] Seats;
        public int Turn { get; private set; }

        public VersusMatch(int seed)
        {
            Seats = new[] { new Seat(seed), new Seat(seed) };
        }

        public Seat CurrentSeat => Seats[Turn];
        public bool Over => Seats[0].Out && Seats[1].Out;

        /// <summary>-1 while playing or on a tie, otherwise 0 or 1.</summary>
        public int Winner
        {
            get
            {
                if (!Over || Seats[0].Board.Score == Seats[1].Board.Score) return -1;
                return Seats[0].Board.Score > Seats[1].Board.Score ? 0 : 1;
            }
        }

        public bool TryPlace(int handIndex, int x, int y)
        {
            if (Over || !CurrentSeat.TryPlace(handIndex, x, y)) return false;
            int other = 1 - Turn;
            if (!Seats[other].Out) Turn = other;
            return true;
        }
    }
}
