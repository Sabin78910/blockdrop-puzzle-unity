using System;
using System.Collections.Generic;
using System.Text;

namespace BlockDrop.Core
{
    public enum GoalKind { Score, Lines, Stones, Gems }

    /// <summary>Where a level sits on the chapter's sawtooth. A hard level is always followed by a relief level
    /// (King found an over-hard level raises short-term spend but loses players; relief keeps momentum).</summary>
    public enum LevelTier { Easy, Normal, Hard, SuperHard, Relief }

    public readonly struct LevelGoal
    {
        public readonly GoalKind Kind;
        public readonly int Target;
        public LevelGoal(GoalKind kind, int target) { Kind = kind; Target = target; }
    }

    /// <summary>Ten levels that share a picture, a name and one new idea for the player to discover.</summary>
    public sealed class Chapter
    {
        public string Name;
        public GoalKind[] Goals;
        public int Special;        // what '*' in a layout becomes: Board.Stone, Gem or Ice
        public bool IceStones;     // a quarter of the plain stones become ice
        public string Intro;       // shown on the chapter's first level: the new thing to learn
    }

    public sealed class LevelDefinition
    {
        public int Number, Seed, MaxMoves, TwoStarScore, ThreeStarScore;
        public LevelTier Tier;
        public LevelGoal[] Goals;
        public (int x, int y, int cell)[] Blockers;

        public int ChapterIndex => (Number - 1) / LevelLibrary.PerChapter;
        public Chapter Chapter => LevelLibrary.Chapters[ChapterIndex];
        /// <summary>The "new!" card for the first level of a chapter, otherwise null.</summary>
        public string Intro => (Number - 1) % LevelLibrary.PerChapter == 0 ? Chapter.Intro : null;
        public bool IsHard => Tier == LevelTier.Hard || Tier == LevelTier.SuperHard;

        /// <summary>Stars for a won level, from its final score (score plus the leftover-moves bonus).</summary>
        public int StarsFor(int finalScore) => finalScore >= ThreeStarScore ? 3 : finalScore >= TwoStarScore ? 2 : 1;
    }

    /// <summary>100 levels in 10 chapters. Each chapter adds a mechanic (lines, stones, gems, ice, double goals)
    /// and follows a sawtooth: easy, rising, a hard spike, relief, a super-hard boss, then a relief level so the
    /// chapter ends on a win. Layouts are hand-drawn shapes, not noise. Every level is tuned by simulated casual
    /// players (<see cref="Playtester"/>) to a target win rate for its tier, and the results are baked in
    /// <see cref="LevelTable"/> so nothing heavy runs on the phone.</summary>
    public static class LevelLibrary
    {
        public const int PerChapter = 10, Count = 100;
        public const int ObstacleColor = Board.Stone;
        /// <summary>Points for every move left when the goal is reached ("bonus blast").</summary>
        public const int MoveBonus = 15;
        public const int ScoreGoalMoves = 20;

        public static readonly Chapter[] Chapters =
        {
            new Chapter { Name = "Sunset",  Goals = new[] { GoalKind.Score },                 Special = Board.Stone, Intro = "Fill a row or column to clear it. Reach the target score!" },
            new Chapter { Name = "Rocket",  Goals = new[] { GoalKind.Lines },                 Special = Board.Stone, Intro = "New goal: clear lines! Every row or column counts." },
            new Chapter { Name = "Heart",   Goals = new[] { GoalKind.Stones },                Special = Board.Stone, Intro = "New goal: clear every stone. Stones vanish with their line." },
            new Chapter { Name = "Gem",     Goals = new[] { GoalKind.Gems },                  Special = Board.Gem,   Intro = "New! Gems. Clear a gem's row or column to collect it." },
            new Chapter { Name = "Peaks",   Goals = new[] { GoalKind.Stones },                Special = Board.Ice,   Intro = "New! Ice. It takes two clears: the first cracks it to stone." },
            new Chapter { Name = "Moon",    Goals = new[] { GoalKind.Score, GoalKind.Gems },  Special = Board.Gem,   Intro = "Double goals! Complete both to win." },
            new Chapter { Name = "Reef",    Goals = new[] { GoalKind.Lines, GoalKind.Stones }, Special = Board.Ice,  Intro = "Ice walls! Clear lines and break every stone." },
            new Chapter { Name = "Blossom", Goals = new[] { GoalKind.Gems, GoalKind.Stones }, Special = Board.Gem, IceStones = true, Intro = "Frozen garden: gems hide among ice. Clear them all." },
            new Chapter { Name = "Castle",  Goals = new[] { GoalKind.Score, GoalKind.Stones }, Special = Board.Ice, IceStones = true, Intro = "Storm the castle: big scores and icy walls." },
            new Chapter { Name = "Crown",   Goals = new[] { GoalKind.Gems, GoalKind.Lines },  Special = Board.Gem, IceStones = true, Intro = "The final chapter. Collect the crown jewels!" },
        };

        /// <summary>Position in the chapter → tier. Two spikes per chapter, each followed by relief.</summary>
        private static readonly LevelTier[] Saw =
        {
            LevelTier.Easy, LevelTier.Easy, LevelTier.Normal, LevelTier.Hard, LevelTier.Relief,
            LevelTier.Normal, LevelTier.Normal, LevelTier.Hard, LevelTier.SuperHard, LevelTier.Relief,
        };

        /// <summary>'#' stone, '*' the chapter's special block, '.' empty. Never more than 5 in a line.</summary>
        public static readonly string[][] Layouts =
        {
            new[] { "##....##", "#......#", "........", "........", "........", "........", "#......#", "##....##" }, // corners
            new[] { "........", "........", "........", "...**...", "...**...", "........", "........", "........" }, // centre
            new[] { "#.......", ".#......", "..*.....", "...#....", "....#...", ".....*..", "......#.", ".......#" }, // diagonal
            new[] { "........", "...#....", "...#....", ".##*##..", "...#....", "...#....", "........", "........" }, // plus
            new[] { "........", "..####..", "........", "...**...", "........", "........", "..####..", "........" }, // bars
            new[] { "........", "........", "..#.#...", "...*.#..", "..#.*...", "...#.#..", "........", "........" }, // checker
            new[] { "........", ".##.##..", "#..#..#.", "#..*..#.", ".#...#..", "..#.#...", "...#....", "........" }, // heart
            new[] { "........", ".#....#.", ".*....*.", "........", "#......#", ".#....#.", "..####..", "........" }, // smile
            new[] { ".#....#.", ".#....#.", "........", ".*....*.", ".*....*.", "........", ".#....#.", "........" }, // pillars
            new[] { "...#....", "..###...", ".#.#.#..", "........", "...*....", "...*....", "........", "........" }, // arrow
            new[] { "........", "..####..", ".#....#.", ".#.**.#.", ".#.**.#.", ".#....#.", "..####..", "........" }, // ring
            new[] { "##......", "......##", "........", "...**...", "........", "##......", "......##", "........" }, // pairs
            new[] { "#.......", "##......", ".##.....", "..*#....", "...##...", "....*#..", "......#.", "........" }, // stairs
            new[] { "........", "........", "#......#", "*......*", "*......*", "#......#", "........", "........" }, // sides
            new[] { "#......#", ".#....#.", "..*..*..", "........", "........", "..*..*..", ".#....#.", "#......#" }, // cross
            new[] { "........", ".##.....", ".##..#..", ".....##.", "..*...#.", ".***....", "........", "........" }, // tetromino
            new[] { "........", "........", "........", "#.#*#.#.", ".#.*.#.#", "........", "........", "........" }, // zipper
            new[] { "........", "...#....", "..#.#...", ".#.*.#..", "..#.#...", "...#....", "........", "........" }, // diamond
            new[] { "###.....", "#.......", "#.......", "...**...", "........", ".......#", ".......#", ".....###" }, // brackets
            new[] { "........", ".####...", "....#...", ".*..#...", ".#..*...", ".####...", "........", "........" }, // spiral
            new[] { "#...#...", "........", "..*...*.", "........", "#...#...", "........", "..*...*.", "........" }, // grid
            new[] { "........", "#...#...", ".#.#.#.#", "..#...#.", "........", "..*...*.", ".*.*.*.*", "*...*..." }, // waves
            new[] { "..#..#..", "...##...", "..####..", ".#.**.#.", "..####..", "..#..#..", "........", "........" }, // invader
            new[] { "...#....", "..#.#...", ".#...#..", ".#.*.#..", ".#.*.#..", ".##.##..", "........", "........" }, // house
        };

        private static readonly Dictionary<int, LevelDefinition> Cache = new Dictionary<int, LevelDefinition>();

        public static LevelTier TierFor(int number) => number <= 3 ? LevelTier.Easy : Saw[(number - 1) % PerChapter];

        /// <summary>Share of simulated casual players who should win on the first try. The spikes get a little
        /// harder each chapter, but never below 40%: hard should feel like "almost", not like a wall.</summary>
        public static double TargetWinRate(int number)
        {
            if (number <= 3) return 0.98;
            int chapter = (number - 1) / PerChapter;
            switch (TierFor(number))
            {
                case LevelTier.Easy: return 0.92;
                case LevelTier.Relief: return 0.95;
                case LevelTier.Normal: return Math.Max(0.70, 0.84 - 0.01 * chapter);
                case LevelTier.Hard: return Math.Max(0.50, 0.66 - 0.015 * chapter);
                default: return Math.Max(0.40, 0.55 - 0.015 * chapter);
            }
        }

        public static LevelDefinition Get(int number)
        {
            if (number < 1 || number > Count) throw new ArgumentOutOfRangeException(nameof(number));
            if (Cache.TryGetValue(number, out var cached)) return cached;
            var def = LevelTable.Has(number) ? LevelTable.Build(number) : Playtester.Tune(number).Definition;
            Cache[number] = def;
            return def;
        }

        /// <summary>The definition before tuning: seed, tier and blockers with <paramref name="trim"/> extra stones removed.</summary>
        public static LevelDefinition Draft(int number, int trim)
        {
            var chapter = Chapters[(number - 1) / PerChapter];
            var tier = TierFor(number);
            var rng = new Random(number * 104729);
            var layout = Layouts[((number - 1) * 7 + 1) % Layouts.Length];
            var stones = new List<(int, int)>();
            var specials = new List<(int, int)>();
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    if (layout[y][x] == '#') stones.Add((x, y));
                    else if (layout[y][x] == '*') specials.Add((x, y));
                }
            Shuffle(stones, rng);
            Shuffle(specials, rng);
            // gem and ice chapters always have at least three of their special block
            while (chapter.Special != Board.Stone && specials.Count < 3 && stones.Count > 0)
            {
                specials.Add(stones[stones.Count - 1]);
                stones.RemoveAt(stones.Count - 1);
            }
            double keep = tier == LevelTier.Easy ? 0.5 : tier == LevelTier.Relief ? 0.4 : tier == LevelTier.Normal ? 0.75 : 1.0;
            if (number <= 3) keep = 0.25;
            // tuning softens a level in steps: first fewer plain stones, then ice cracked to stone, then fewer specials
            int kept = (int)Math.Round(stones.Count * keep), left = trim;
            int drop = Math.Min(left, kept);
            kept -= drop; left -= drop;
            int cracked = chapter.Special == Board.Ice ? Math.Min(left, specials.Count) : 0;
            left -= cracked;
            while (left-- > 0 && specials.Count > 2) specials.RemoveAt(specials.Count - 1);
            var blockers = new List<(int, int, int)>();
            for (int i = 0; i < kept; i++)
            {
                var (x, y) = stones[i];
                blockers.Add((x, y, chapter.IceStones && i % 4 == 0 && trim < 6 ? Board.Ice : Board.Stone));
            }
            for (int i = 0; i < specials.Count; i++)
                blockers.Add((specials[i].Item1, specials[i].Item2, i < cracked ? Board.Stone : chapter.Special));
            // the stones goal needs something to clear even after heavy trimming
            if (Array.IndexOf(chapter.Goals, GoalKind.Stones) >= 0 && !blockers.Exists(b => b.Item3 != Board.Gem))
            {
                var spot = stones.Count > 0 ? stones[0] : (3, 3);
                blockers.RemoveAll(b => b.Item1 == spot.Item1 && b.Item2 == spot.Item2);
                blockers.Add((spot.Item1, spot.Item2, Board.Stone));
            }
            blockers.Sort();
            return new LevelDefinition { Number = number, Seed = 1000 + number * 7919, Tier = tier, Blockers = blockers.ToArray() };
        }

        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Goals with their targets: stones and gems mean "all of them", score and lines come from tuning.</summary>
        public static LevelGoal[] GoalsFor(LevelDefinition def, int scoreTarget, int linesTarget)
        {
            var kinds = def.Chapter.Goals;
            var goals = new LevelGoal[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                int target = kinds[i] switch
                {
                    GoalKind.Score => scoreTarget,
                    GoalKind.Lines => linesTarget,
                    GoalKind.Stones => CountBlockers(def, c => c == Board.Stone || c == Board.Ice),
                    _ => CountBlockers(def, c => c == Board.Gem),
                };
                goals[i] = new LevelGoal(kinds[i], target);
            }
            return goals;
        }

        private static int CountBlockers(LevelDefinition def, Func<int, bool> match)
        {
            int n = 0;
            foreach (var b in def.Blockers) if (match(b.cell)) n++;
            return n;
        }

        /// <summary>A fresh seat for the level. Levels deal whole fair hands: every piece fits when it is dealt.</summary>
        public static Seat NewSeat(LevelDefinition def)
        {
            var seat = new Seat(def.Seed, 8, wholeHands: true);
            foreach (var (x, y, cell) in def.Blockers) seat.Board.SetCell(x, y, cell);
            // the first hand was dealt on an empty board; deal it again now the blockers are down
            seat.Dealer.Redeal(p => seat.Board.CanPlaceAnywhere(p));
            return seat;
        }
    }

    public enum LevelState { Playing, Won, Lost }

    public sealed class LevelSession
    {
        public const int ContinueMoves = 5, MaxContinues = 2;
        public readonly LevelDefinition Definition;
        public readonly Seat Seat;
        /// <summary>Moves added by the helping hand or by continuing.</summary>
        public int ExtraMoves { get; private set; }
        public int Continues { get; private set; }

        /// <param name="bonusMoves">The visible "helping hand" after repeated losses.</param>
        public LevelSession(int number, int bonusMoves = 0)
        {
            Definition = LevelLibrary.Get(number);
            Seat = LevelLibrary.NewSeat(Definition);
            ExtraMoves = Math.Max(0, bonusMoves);
        }

        public int MaxMoves => Definition.MaxMoves + ExtraMoves;
        public int MovesLeft => Math.Max(0, MaxMoves - Seat.Moves);

        /// <summary>How far a goal has come: points, lines, or stones/gems cleared so far.</summary>
        public int Progress(int goal)
        {
            var g = Definition.Goals[goal];
            var b = Seat.Board;
            return g.Kind switch
            {
                GoalKind.Score => b.Score,
                GoalKind.Lines => b.LinesCleared,
                GoalKind.Stones => g.Target - b.Count(Board.Stone) - b.Count(Board.Ice),
                _ => g.Target - b.Count(Board.Gem),
            };
        }

        public bool GoalMet(int goal) => Progress(goal) >= Definition.Goals[goal].Target;

        public bool AllGoalsMet
        {
            get
            {
                for (int i = 0; i < Definition.Goals.Length; i++) if (!GoalMet(i)) return false;
                return true;
            }
        }

        /// <summary>Won the moment every goal is met, so a win never waits on spare moves. Lost when moves run out
        /// (a stuck hand is reshuffled for free, so Out only remains if even a single block cannot fit).</summary>
        public LevelState State => AllGoalsMet ? LevelState.Won : MovesLeft == 0 || Seat.Out ? LevelState.Lost : LevelState.Playing;

        public int Bonus => State == LevelState.Won ? MovesLeft * LevelLibrary.MoveBonus : 0;
        public int FinalScore => Seat.Board.Score + Bonus;
        public int Stars => State == LevelState.Won ? Definition.StarsFor(FinalScore) : 0;

        /// <summary>Times the board had no room and the hand was reshuffled for free.</summary>
        public int Shuffles { get; private set; }

        public bool TryPlace(int handIndex, int x, int y)
        {
            if (State != LevelState.Playing || !Seat.TryPlace(handIndex, x, y)) return false;
            // like Candy Crush's free shuffle: a level is never lost to a stuck hand, only to running out of moves
            if (Seat.Out && MovesLeft > 0 && !AllGoalsMet && Seat.Revive()) Shuffles++;
            return true;
        }

        public bool CanContinue => State == LevelState.Lost && Continues < MaxContinues;

        /// <summary>Five more moves. Paid for with coins in the game.</summary>
        public bool Continue()
        {
            if (!CanContinue) return false;
            if (Seat.Out) Seat.Revive();
            ExtraMoves += ContinueMoves;
            Continues++;
            return State == LevelState.Playing;
        }

        /// <summary>Puts back moves granted earlier in a saved run.</summary>
        public void RestoreExtra(int extraMoves, int continues)
        {
            ExtraMoves = Math.Max(0, extraMoves);
            Continues = Math.Max(0, continues);
        }

        /// <summary>The honest near-miss line after a loss: the goal that was closest, and exactly how close.
        /// Null if the player was not close (under 75% of the way), so we never fake an "almost".</summary>
        public string NearMiss()
        {
            if (State != LevelState.Lost) return null;
            string best = null;
            double bestShare = 0.75;
            for (int i = 0; i < Definition.Goals.Length; i++)
            {
                var g = Definition.Goals[i];
                int left = g.Target - Progress(i);
                if (left <= 0) continue;
                double share = (double)Progress(i) / Math.Max(1, g.Target);
                if (share < bestShare) continue;
                bestShare = share;
                best = g.Kind switch
                {
                    GoalKind.Score => Loc.F("So close! Only {0} points to go.", left),
                    GoalKind.Lines => left == 1 ? Loc.L("So close! Just 1 more line.") : Loc.F("So close! Just {0} more lines.", left),
                    GoalKind.Stones => left == 1 ? Loc.L("So close! Only 1 stone left.") : Loc.F("So close! Only {0} stones left.", left),
                    _ => left == 1 ? Loc.L("So close! Only 1 gem left.") : Loc.F("So close! Only {0} gems left.", left),
                };
            }
            return best;
        }
    }

    /// <summary>Which levels are unlocked and their best stars. Serialized to a short string for PlayerPrefs.</summary>
    public sealed class Progress
    {
        private readonly int[] _stars = new int[LevelLibrary.Count + 1];

        public int StarsFor(int level) => _stars[level];
        public bool IsUnlocked(int level) => level == 1 || (level <= LevelLibrary.Count && _stars[level - 1] > 0);
        public int TotalStars { get { int t = 0; foreach (var s in _stars) t += s; return t; } }

        /// <summary>The furthest level the player can play: where the level map opens.</summary>
        public int Frontier
        {
            get
            {
                int n = 1;
                while (n < LevelLibrary.Count && IsUnlocked(n + 1)) n++;
                return n;
            }
        }

        /// <summary>Keeps the best result. Returns how many stars are new, which is what earns coins, so replaying
        /// a level is never a grind for currency.</summary>
        public int Record(int level, int stars)
        {
            int gained = Math.Max(0, stars - _stars[level]);
            if (stars > _stars[level]) _stars[level] = stars;
            return gained;
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

    /// <summary>Simulated casual players, the way King tests levels with bots before release. The playtester
    /// looks one piece ahead, likes clearing lines and blockers, and makes the mistakes people make: 40% of the
    /// time it picks a decent move rather than the best. Tuning plays each draft level many times and sets the
    /// moves so the tier's target share of players win.</summary>
    public static class Playtester
    {
        public const int Runs = 64, MoveCap = 50, MaxTrim = 24;

        public sealed class Result
        {
            public LevelDefinition Definition;
            public int ScoreTarget, LinesTarget, Trim;
            public double WinRate;
        }

        public static bool TryMove(Board b, Piece[] hand, Random rng, out int handIndex, out int x, out int y)
        {
            var moves = new List<(int value, int i, int x, int y)>();
            int blockers = b.Count(Board.Stone) + b.Count(Board.Ice) * 2 + b.Count(Board.Gem) * 2;
            for (int i = 0; i < hand.Length; i++)
            {
                var p = hand[i];
                if (p == null) continue;
                for (int px = 0; px < b.Size; px++)
                    for (int py = 0; py < b.Size; py++)
                    {
                        if (!b.CanPlace(p, px, py)) continue;
                        var sim = b.Clone();
                        int points = sim.Place(p, px, py).Points;
                        int after = sim.Count(Board.Stone) + sim.Count(Board.Ice) * 2 + sim.Count(Board.Gem) * 2;
                        moves.Add((points * 100 + (blockers - after) * 300 + Aim(b, p, px, py) * 30 + Touching(b, p, px, py) * 4 + rng.Next(60), i, px, py));
                    }
            }
            handIndex = x = y = -1;
            if (moves.Count == 0) return false;
            moves.Sort((m1, m2) => m2.value.CompareTo(m1.value));
            int pick = rng.NextDouble() < 0.6 ? 0 : rng.Next(Math.Min(4, moves.Count));
            (_, handIndex, x, y) = moves[pick];
            return true;
        }

        /// <summary>Players chase the goal: cells dropped into rows and columns that hold blockers count extra.</summary>
        private static int Aim(Board b, Piece p, int ox, int oy)
        {
            int n = 0;
            foreach (var (cx, cy) in p.Cells)
            {
                int x = ox + cx, y = oy + cy;
                for (int k = 0; k < b.Size; k++)
                {
                    if (b[k, y] >= Board.Stone) n++;
                    if (b[x, k] >= Board.Stone) n++;
                }
            }
            return n;
        }

        private static int Touching(Board b, Piece p, int ox, int oy)
        {
            int n = 0;
            foreach (var (cx, cy) in p.Cells)
            {
                int x = ox + cx, y = oy + cy;
                if (x == 0 || b.IsFilled(x - 1, y)) n++;
                if (x == b.Size - 1 || b.IsFilled(x + 1, y)) n++;
                if (y == 0 || b.IsFilled(x, y - 1)) n++;
                if (y == b.Size - 1 || b.IsFilled(x, y + 1)) n++;
            }
            return n;
        }

        private struct Snap { public int Score, Lines, Stones, Gems; }

        /// <summary>Plays one run to the move cap (or until stuck) and records the board after every move.</summary>
        private static List<Snap> Run(LevelDefinition draft, int run)
        {
            var seat = LevelLibrary.NewSeat(draft);
            var rng = new Random(draft.Number * 7907 + run * 31);
            var snaps = new List<Snap>();
            while (seat.Moves < MoveCap && TryMove(seat.Board, seat.Dealer.Hand, rng, out int i, out int x, out int y))
            {
                seat.TryPlace(i, x, y);
                if (seat.Out && !seat.Revive()) break; // the free shuffle, as in LevelSession
                var b = seat.Board;
                snaps.Add(new Snap { Score = b.Score, Lines = b.LinesCleared, Stones = b.Count(Board.Stone) + b.Count(Board.Ice), Gems = b.Count(Board.Gem) });
            }
            return snaps;
        }

        private static int Median(List<int> v)
        {
            v.Sort();
            return v.Count == 0 ? 0 : v[v.Count / 2];
        }

        private static int Round(int v, int step) => Math.Max(step, (v + step / 2) / step * step);

        /// <summary>Tunes one level. Deterministic: same number, same result (the baked table is checked against it).</summary>
        public static Result Tune(int number)
        {
            double want = LevelLibrary.TargetWinRate(number);
            Result fallback = null;
            for (int trim = 0; trim <= MaxTrim; trim++)
            {
                var def = LevelLibrary.Draft(number, trim);
                var runs = new List<List<Snap>>();
                for (int r = 0; r < Runs; r++) runs.Add(Run(def, r));

                // score and lines targets: what a typical player reaches in ScoreGoalMoves moves
                var scores = new List<int>();
                var lines = new List<int>();
                foreach (var s in runs)
                {
                    var at = s.Count == 0 ? new Snap() : s[Math.Min(s.Count, LevelLibrary.ScoreGoalMoves) - 1];
                    scores.Add(at.Score);
                    lines.Add(at.Lines);
                }
                int scoreTarget = Round(Median(scores), 10), linesTarget = Math.Max(2, Median(lines));
                def.Goals = LevelLibrary.GoalsFor(def, scoreTarget, linesTarget);

                // the move on which each run met every goal (int.MaxValue = never)
                var done = new List<int>();
                foreach (var s in runs) done.Add(CompletionMove(s, def.Goals));
                done.Sort();
                int k = (int)Math.Ceiling(want * Runs) - 1;
                int moves = done[k];
                if (moves > LongestLevel(def.Tier))
                {
                    if (fallback == null || trim == MaxTrim) fallback = Finish(def, runs, done, Math.Min(MoveCap, LongestLevel(def.Tier) + 6), scoreTarget, linesTarget, trim);
                    continue; // too hard for this tier: take a stone out and try again
                }
                return Finish(def, runs, done, Math.Max(8, moves), scoreTarget, linesTarget, trim);
            }
            return fallback;
        }

        /// <summary>Levels stay snack sized (about two to four minutes). A level that needs more moves than this
        /// to hit its win rate is softened instead of padded.</summary>
        public static int LongestLevel(LevelTier tier) => tier switch
        {
            LevelTier.Easy => 28,
            LevelTier.Relief => 26,
            LevelTier.Normal => 32,
            _ => 36,
        };

        private static Result Finish(LevelDefinition def, List<List<Snap>> runs, List<int> done, int moves, int scoreTarget, int linesTarget, int trim)
        {
            def.MaxMoves = moves;
            var finals = new List<int>();
            foreach (var s in runs)
            {
                int m = CompletionMove(s, def.Goals);
                if (m <= moves) finals.Add(s[m - 1].Score + (moves - m) * LevelLibrary.MoveBonus);
            }
            finals.Sort();
            int wins = finals.Count;
            def.TwoStarScore = wins == 0 ? 1 : finals[wins * 2 / 5];
            def.ThreeStarScore = Math.Max(def.TwoStarScore + 10, wins == 0 ? 1 : finals[Math.Min(wins - 1, wins * 4 / 5)]);
            return new Result { Definition = def, ScoreTarget = scoreTarget, LinesTarget = linesTarget, Trim = trim, WinRate = (double)wins / runs.Count };
        }

        private static int CompletionMove(List<Snap> s, LevelGoal[] goals)
        {
            for (int m = 0; m < s.Count; m++)
            {
                bool all = true;
                foreach (var g in goals)
                {
                    bool met = g.Kind switch
                    {
                        GoalKind.Score => s[m].Score >= g.Target,
                        GoalKind.Lines => s[m].Lines >= g.Target,
                        GoalKind.Stones => s[m].Stones == 0,
                        _ => s[m].Gems == 0,
                    };
                    if (!met) { all = false; break; }
                }
                if (all) return m + 1;
            }
            return int.MaxValue;
        }

        /// <summary>Source for <see cref="LevelTable"/>, printed by tools/LevelTuner.</summary>
        public static string TableRow(Result r)
        {
            var d = r.Definition;
            return $"{{ {d.Number}, {d.MaxMoves}, {r.ScoreTarget}, {r.LinesTarget}, {d.TwoStarScore}, {d.ThreeStarScore}, {r.Trim} }}, // {d.Tier}, {Math.Round(r.WinRate * 100)}% win";
        }
    }
}
