using System;
using System.Collections.Generic;
using System.Text;

namespace BlockDrop.Core
{
    /// <summary>Deals a hand of three pieces; a new hand arrives once all three are used.</summary>
    public sealed class Dealer
    {
        public const int HandSize = 3;
        private const int FairRedraws = 12;
        private PieceGenerator _gen;
        private readonly Func<Piece, bool> _fits; // null = classic random dealing
        public readonly Piece[] Hand = new Piece[HandSize];

        /// <param name="fits">When given, every new hand contains at least one piece that fits ("every hand is playable").</param>
        public Dealer(int seed, Func<Piece, bool> fits = null)
        {
            _gen = new PieceGenerator(seed);
            _fits = fits;
            Deal();
        }

        private void Deal()
        {
            for (int i = 0; i < HandSize; i++) Hand[i] = _gen.Next();
            if (_fits != null) EnsurePlayable(_fits);
        }

        /// <summary>Redraws until a piece fits; as a last resort the first piece becomes a single block, which
        /// always fits because a completely full board would already have cleared its lines.</summary>
        private void EnsurePlayable(Func<Piece, bool> fits)
        {
            for (int t = 0; t < FairRedraws && !Array.Exists(Hand, p => p != null && fits(p)); t++)
                for (int i = 0; i < HandSize; i++) Hand[i] = _gen.Next();
            if (!Array.Exists(Hand, p => p != null && fits(p))) Hand[0] = new Piece(Piece.Shapes[0], Hand[0]?.ColorIndex ?? 0);
        }

        /// <summary>A fresh hand guaranteed to be playable (used by "Second chance").</summary>
        public void Redeal(Func<Piece, bool> fits)
        {
            for (int i = 0; i < HandSize; i++) Hand[i] = _gen.Next();
            EnsurePlayable(fits);
        }

        public int Seed => _gen.Seed;
        public int Draws => _gen.Draws;

        /// <summary>Resumes a saved game: the same piece sequence continues after <paramref name="draws"/> pieces.</summary>
        public void Restore(int draws, Piece[] hand)
        {
            if (hand == null || hand.Length != HandSize) throw new ArgumentException("Hand needs " + HandSize + " slots");
            _gen = PieceGenerator.Resume(_gen.Seed, draws);
            Array.Copy(hand, Hand, HandSize);
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
        public int Revives { get; private set; }

        public Seat(int seed, int size = 8, bool fairHands = false)
        {
            Board = new Board(size);
            Dealer = new Dealer(seed, fairHands ? p => Board.CanPlaceAnywhere(p) : (Func<Piece, bool>)null);
        }

        /// <summary>Second chance after running out of moves: a fresh hand that fits the current board.</summary>
        public bool Revive()
        {
            if (!Out) return false;
            Dealer.Redeal(p => Board.CanPlaceAnywhere(p));
            Out = Board.IsGameOver(Dealer.Hand);
            Revives++;
            return !Out;
        }

        /// <summary>Resumes a saved game's counters; board and hand are restored separately.</summary>
        public void RestoreCounters(int moves, int revives)
        {
            Moves = moves;
            Revives = revives;
            Out = Board.IsGameOver(Dealer.Hand);
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

    public enum BotLevel { Easy, Medium, Hard }

    /// <summary>Greedy player: used to calibrate level targets (every level is beatable) and for hints.</summary>
    public static class Bot
    {
        /// <summary>Easy: random legal move. Medium: greedy. Hard: two-move lookahead within the hand.</summary>
        public static bool TryMove(Board board, Piece[] hand, BotLevel level, Random rng, out int handIndex, out int x, out int y)
        {
            switch (level)
            {
                case BotLevel.Easy: return TryRandomMove(board, hand, rng, out handIndex, out x, out y);
                case BotLevel.Hard: return TryLookaheadMove(board, hand, out handIndex, out x, out y);
                default: return TryBestMove(board, hand, out handIndex, out x, out y);
            }
        }

        private static bool TryRandomMove(Board board, Piece[] hand, Random rng, out int handIndex, out int x, out int y)
        {
            var moves = new List<(int, int, int)>();
            for (int i = 0; i < hand.Length; i++)
            {
                if (hand[i] == null) continue;
                for (int px = 0; px < board.Size; px++)
                    for (int py = 0; py < board.Size; py++)
                        if (board.CanPlace(hand[i], px, py)) moves.Add((i, px, py));
            }
            handIndex = x = y = -1;
            if (moves.Count == 0) return false;
            (handIndex, x, y) = moves[rng.Next(moves.Count)];
            return true;
        }

        private static bool TryLookaheadMove(Board board, Piece[] hand, out int handIndex, out int x, out int y)
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
                        // best greedy follow-up with the rest of the hand
                        var rest = (Piece[])hand.Clone();
                        rest[i] = null;
                        bool any = false;
                        foreach (var q in rest) if (q != null) { any = true; break; }
                        if (any)
                        {
                            if (TryBestMove(sim, rest, out int j, out int qx, out int qy))
                            {
                                var sim2 = sim.Clone();
                                score += sim2.Place(rest[j], qx, qy).Points * 100 - TrappedCells(sim2) * 8;
                            }
                            else score -= 10000; // this move strands the rest of the hand
                        }
                        if (score > best) { best = score; handIndex = i; x = px; y = py; }
                    }
            }
            return handIndex >= 0;
        }

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

    /// <summary>Hard and super hard levels are labelled in game, like Candy Crush and Royal Match do.</summary>
    public enum LevelTier { Normal, Hard, SuperHard }

    public sealed class LevelDefinition
    {
        public int Number, Seed, MaxMoves, TargetScore, TwoStarScore, ThreeStarScore;
        public LevelTier Tier;
        public (int x, int y)[] Obstacles;

        public int StarsFor(int score) =>
            score >= ThreeStarScore ? 3 : score >= TwoStarScore ? 2 : score >= TargetScore ? 1 : 0;
    }

    /// <summary>Endless deterministic levels: every player gets the same level N. Levels 1-30 are the launch set,
    /// unchanged so saved stars keep their meaning. From 31 on, levels are generated from their number in chapters
    /// of 10 with a sawtooth curve (easy opener, hard 5th, dip, super hard 10th) on a slow overall rise that levels
    /// off, so deep levels stay varied rather than impossible. Targets are calibrated against the greedy bot,
    /// so every level is winnable.</summary>
    public static class LevelLibrary
    {
        public const int Handmade = 30;
        public const int Count = 1000000; // effectively endless; nobody reaches the end
        public const int ChapterSize = 10;
        public const int ObstacleColor = 5;
        private const int CacheLimit = 64;
        private static readonly Dictionary<int, LevelDefinition> Cache = new Dictionary<int, LevelDefinition>();

        public static LevelDefinition Get(int number)
        {
            if (number < 1 || number > Count) throw new ArgumentOutOfRangeException(nameof(number));
            if (Cache.TryGetValue(number, out var cached)) return cached;

            var def = number <= Handmade ? Launch(number) : Generated(number);
            int bot = Bot.Play(NewSeat(def), def.MaxMoves);
            double difficulty = number <= Handmade ? LaunchRatio(number) : TargetRatio(number);
            def.TargetScore = Math.Max(1, Math.Min(bot, (int)(bot * difficulty)));
            def.TwoStarScore = Math.Max(def.TargetScore, (def.TargetScore + bot) / 2);
            def.ThreeStarScore = Math.Max(def.TwoStarScore, bot);
            if (Cache.Count >= CacheLimit) Cache.Clear();
            Cache[number] = def;
            return def;
        }

        public static LevelTier TierOf(int number)
        {
            if (number <= ChapterSize) return LevelTier.Normal; // the first chapter is a warm-up
            int p = (number - 1) % ChapterSize;
            return p == ChapterSize - 1 ? LevelTier.SuperHard : p == 4 ? LevelTier.Hard : LevelTier.Normal;
        }

        public static Seat NewSeat(LevelDefinition def)
        {
            var seat = new Seat(def.Seed);
            foreach (var (x, y) in def.Obstacles) seat.Board.SetCell(x, y, ObstacleColor);
            return seat;
        }

        /// <summary>The first levels are a warm-up: no obstacles for three levels, then one more every other level or so,
        /// and a target of only 30% of what the bot scores, creeping up to 60% by level 30 so it joins level 31 smoothly.
        /// From level 11 the chapter sawtooth (hard 5th, super hard 10th) starts, once players have the hang of it.</summary>
        private static LevelDefinition Launch(int number) => new LevelDefinition
        {
            Number = number,
            Seed = 1000 + number * 7919,
            MaxMoves = 30 - number / 10,
            Tier = TierOf(number),
            Obstacles = MakeObstacles(number),
        };

        private static double LaunchRatio(int number) =>
            0.20 + 0.40 * (number - 1) / (Handmade - 1) + (number > ChapterSize ? 0.05 * Saw[(number - 1) % ChapterSize] : 0);

        // position inside a chapter of 10: 0 = breather ... 4 = hard, 5 = dip ... 9 = super hard
        private static readonly double[] Saw = { 0.0, 0.25, 0.45, 0.6, 1.0, 0.3, 0.5, 0.65, 0.8, 1.3 };

        /// <summary>0 at level 31, rising quickly at first and levelling off towards 1.</summary>
        private static double Depth(int number) => 1 - Math.Exp(-(number - Handmade) / 300.0);

        private static double TargetRatio(int number)
        {
            double r = 0.62 + 0.2 * Depth(number) + 0.08 * Saw[(number - 1) % ChapterSize];
            return Math.Min(0.95, r);
        }

        private static LevelDefinition Generated(int number)
        {
            int seed = Mix(number);
            var rng = new Random(seed);
            double depth = Depth(number), saw = Saw[(number - 1) % ChapterSize];
            int obstacles = Math.Min(22, 8 + (int)(10 * depth + 4 * saw) + rng.Next(3));
            return new LevelDefinition
            {
                Number = number,
                Seed = seed,
                Tier = TierOf(number),
                MaxMoves = Math.Max(22, 34 - (int)(6 * saw) + rng.Next(-2, 3)),
                Obstacles = Layout(rng, obstacles),
            };
        }

        /// <summary>Stable integer hash, so a level's seed never depends on the runtime's own hashing.</summary>
        private static int Mix(int n)
        {
            unchecked
            {
                uint z = (uint)n * 0x9E3779B9u;
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                return (int)((z ^ (z >> 16)) & 0x7FFFFFFF);
            }
        }

        private static (int, int)[] MakeObstacles(int number)
        {
            var rng = new Random(number * 104729);
            int count = number <= 3 ? 0 : Math.Min((number - 3) * 2 / 3, 22);
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

        /// <summary>Generated layouts vary in shape so chapters do not look alike: scattered, mirrored,
        /// four-way symmetric, diagonal or pillars. The 5-per-line cap keeps every row and column clearable.</summary>
        private static (int, int)[] Layout(Random rng, int count)
        {
            var set = new HashSet<(int, int)>();
            var rows = new int[8];
            var cols = new int[8];
            bool TryAdd(params (int x, int y)[] group)
            {
                var cells = new HashSet<(int, int)>(group);
                var r = (int[])rows.Clone();
                var c = (int[])cols.Clone();
                foreach (var (x, y) in cells)
                {
                    if (set.Contains((x, y)) || ++r[y] > 5 || ++c[x] > 5) return false;
                }
                foreach (var cell in cells) set.Add(cell);
                Array.Copy(r, rows, 8); Array.Copy(c, cols, 8);
                return true;
            }

            int style = rng.Next(5);
            for (int tries = 0; set.Count < count && tries < 400; tries++)
            {
                int x = rng.Next(8), y = rng.Next(8);
                switch (style)
                {
                    case 1: if (set.Count + 2 <= count) TryAdd((x, y), (7 - x, y)); break;                             // mirrored
                    case 2: if (set.Count + 4 <= count) TryAdd((x, y), (7 - x, y), (x, 7 - y), (7 - x, 7 - y)); break; // four-way
                    case 3: { int d = rng.Next(8), o = rng.Next(-1, 2); if (d + o >= 0 && d + o < 8) TryAdd((d, d + o)); else TryAdd((7 - d, d)); break; } // diagonals
                    case 4: { int px = rng.Next(4) * 2 + 1, py = rng.Next(7); TryAdd((px, py), (px, py + 1)); break; } // pillars
                    default: TryAdd((x, y)); break;
                }
            }
            for (int tries = 0; set.Count < count && tries < 1000; tries++) TryAdd((rng.Next(8), rng.Next(8))); // top up
            var list = new List<(int, int)>(set);
            list.Sort();
            return list.ToArray();
        }
    }

    /// <summary>Gentle rubber band: each lost try on a level lowers its target by 10% (up to 30%), so a stuck player
    /// gets a win soon instead of quitting. Resets on a win. Stars still need the full-strength scores.</summary>
    public static class LevelAssist
    {
        public const int MaxFails = 3;
        private const double PerFail = 0.10;

        public static LevelDefinition Apply(LevelDefinition def, int fails)
        {
            fails = Math.Min(Math.Max(fails, 0), MaxFails);
            if (fails == 0) return def;
            int target = Math.Max(1, (int)(def.TargetScore * (1 - PerFail * fails)));
            return new LevelDefinition
            {
                Number = def.Number, Seed = def.Seed, MaxMoves = def.MaxMoves, Tier = def.Tier, Obstacles = def.Obstacles,
                TargetScore = target, TwoStarScore = Math.Max(def.TwoStarScore, target), ThreeStarScore = Math.Max(def.ThreeStarScore, target),
            };
        }

        /// <summary>Coins for a clear: 5 per new star, plus 5 for the first time through a level.</summary>
        public static int Coins(int oldStars, int newStars) => newStars > oldStars ? 5 * (newStars - oldStars) + (oldStars == 0 ? 5 : 0) : 0;
    }

    /// <summary>Remembers the last level the player lost and how many times in a row (one short string).</summary>
    public sealed class FailTracker
    {
        public int Level { get; private set; }
        public int Fails { get; private set; }

        public int FailsFor(int level) => level == Level ? Fails : 0;

        public void Record(int level, bool won)
        {
            if (won) { if (level == Level) { Level = 0; Fails = 0; } return; }
            Fails = level == Level ? Math.Min(Fails + 1, LevelAssist.MaxFails) : 1;
            Level = level;
        }

        public string Serialize() => Level + "," + Fails;

        public static FailTracker Parse(string data)
        {
            var t = new FailTracker();
            var f = (data ?? "").Split(',');
            if (f.Length == 2 && int.TryParse(f[0], out int l) && int.TryParse(f[1], out int n) && l >= 0 && l <= LevelLibrary.Count && n >= 0 && n <= LevelAssist.MaxFails)
            { t.Level = l; t.Fails = l == 0 ? 0 : n; }
            return t;
        }
    }

    public enum LevelState { Playing, Won, Lost }

    public sealed class LevelSession
    {
        public readonly LevelDefinition Definition;
        public readonly Seat Seat;

        public LevelSession(int number, int fails = 0)
        {
            Definition = LevelAssist.Apply(LevelLibrary.Get(number), fails);
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

    /// <summary>Which levels are unlocked and their best stars. Serialized to a short string for PlayerPrefs:
    /// one digit per level up to the furthest one played, so it grows with the player, not with the level count.</summary>
    public sealed class Progress
    {
        private readonly List<int> _stars = new List<int> { 0 }; // index 0 unused

        public int StarsFor(int level) => level > 0 && level < _stars.Count ? _stars[level] : 0;
        public bool IsUnlocked(int level) => level == 1 || (level >= 2 && level <= LevelLibrary.Count && StarsFor(level - 1) > 0);
        public int TotalStars { get; private set; }

        /// <summary>The furthest level the player can play now.</summary>
        public int Frontier
        {
            get
            {
                int n = 1;
                while (n < LevelLibrary.Count && StarsFor(n) > 0) n++;
                return n;
            }
        }

        public void Record(int level, int stars)
        {
            if (level < 1 || level > LevelLibrary.Count || stars <= StarsFor(level)) return;
            while (_stars.Count <= level) _stars.Add(0);
            TotalStars += stars - _stars[level];
            _stars[level] = stars;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            for (int i = 1; i < _stars.Count; i++) sb.Append(_stars[i]);
            return sb.ToString();
        }

        public static Progress Parse(string data)
        {
            var p = new Progress();
            if (string.IsNullOrEmpty(data)) return p;
            for (int i = 0; i < data.Length && i < LevelLibrary.Count; i++)
                if (data[i] >= '0' && data[i] <= '3') p.Record(i + 1, data[i] - '0');
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
