using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlockDrop.Core
{
    public enum MissionKind { ClearLines, ScorePoints, PlaceBlocks, ComboReach, PlayGames, EarnStars }

    public sealed class Mission
    {
        public MissionKind Kind;
        public int Target, Reward;

        public string Text => Kind switch
        {
            MissionKind.ClearLines => Loc.F("Clear {0} lines", Target),
            MissionKind.ScorePoints => Loc.F("Score {0} points in total", Target),
            MissionKind.PlaceBlocks => Loc.F("Place {0} pieces", Target),
            MissionKind.ComboReach => Loc.F("Reach a x{0} combo", Target),
            MissionKind.PlayGames => Loc.F("Finish {0} games", Target),
            _ => Loc.F("Earn {0} level stars", Target),
        };
    }

    /// <summary>Lifetime statistics that drive achievements. "Best" stats keep the maximum, the others add up.</summary>
    public enum Stat { LinesCleared, BestCombo, GamesPlayed, BestGameScore, LevelStars, HardAiWins, BestStreak }

    public sealed class Achievement
    {
        public string Name, Text;
        public Stat Stat;
        public int Target, Reward;
    }

    public sealed class Theme
    {
        public string Name;
        public int Price;
        public string[] Blocks;   // 5 hex colours
        public string BackgroundTint;
    }

    /// <summary>Fair, skill-based progression: daily streak, three daily missions, coins and cosmetic
    /// themes. No randomised paid rewards. Pure logic (dates are passed in) so it is fully testable.</summary>
    public sealed class PlayerMeta
    {
        public static readonly Theme[] Themes =
        {
            new Theme { Name = "Jewel",  Price = 0,   Blocks = new[] { "FF4D6D", "FFB627", "2EE59D", "2EC4F1", "A66CFF" }, BackgroundTint = "FFFFFF" },
            new Theme { Name = "Neon",   Price = 150, Blocks = new[] { "FF2E93", "F9F871", "00F5A0", "00D9FF", "B967FF" }, BackgroundTint = "6B5BA8" },
            new Theme { Name = "Candy",  Price = 250, Blocks = new[] { "FF8FAB", "FFD6A5", "9BF6C4", "A0E7FF", "CDB4FF" }, BackgroundTint = "FFC2E2" },
            new Theme { Name = "Ocean",  Price = 350, Blocks = new[] { "00B4D8", "48CAE4", "2EC4B6", "0077B6", "90E0EF" }, BackgroundTint = "8FD3FF" },
            new Theme { Name = "Sunset", Price = 500, Blocks = new[] { "FF6B35", "F7C59F", "EF476F", "FFD166", "C77DFF" }, BackgroundTint = "FFB38A" },
        };

        private static readonly (MissionKind kind, int[] targets, int reward)[] Pool =
        {
            (MissionKind.ClearLines, new[] { 10, 20, 30 }, 30),
            (MissionKind.ScorePoints, new[] { 500, 1000, 2000 }, 30),
            (MissionKind.PlaceBlocks, new[] { 30, 50, 80 }, 20),
            (MissionKind.ComboReach, new[] { 2, 3, 4 }, 40),
            (MissionKind.PlayGames, new[] { 2, 3, 5 }, 25),
            (MissionKind.EarnStars, new[] { 2, 3, 5 }, 35),
        };

        public int Coins { get; private set; }
        public void AddCoins(int amount) { if (amount > 0) Coins += amount; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public string LastCheckIn { get; private set; } = "";
        public string MissionDay { get; private set; } = "";
        public readonly int[] Progress = new int[3];
        public readonly bool[] Claimed = new bool[3];
        public int OwnedThemes { get; private set; } = 1; // bit mask, Jewel owned
        public int ThemeIndex { get; private set; }

        public static readonly Achievement[] Achievements =
        {
            new Achievement { Name = "First Clear",    Text = "Clear your first line",     Stat = Stat.LinesCleared,  Target = 1,    Reward = 10 },
            new Achievement { Name = "Line Cleaner",   Text = "Clear 100 lines",           Stat = Stat.LinesCleared,  Target = 100,  Reward = 50 },
            new Achievement { Name = "Line Legend",    Text = "Clear 1,000 lines",         Stat = Stat.LinesCleared,  Target = 1000, Reward = 200 },
            new Achievement { Name = "Combo Starter",  Text = "Reach a x3 combo",          Stat = Stat.BestCombo,     Target = 3,    Reward = 30 },
            new Achievement { Name = "Combo King",     Text = "Reach a x6 combo",          Stat = Stat.BestCombo,     Target = 6,    Reward = 100 },
            new Achievement { Name = "Warming Up",     Text = "Finish 10 games",           Stat = Stat.GamesPlayed,   Target = 10,   Reward = 30 },
            new Achievement { Name = "Dedicated",      Text = "Finish 100 games",          Stat = Stat.GamesPlayed,   Target = 100,  Reward = 150 },
            new Achievement { Name = "High Scorer",    Text = "Score 1,000 in one game",   Stat = Stat.BestGameScore, Target = 1000, Reward = 50 },
            new Achievement { Name = "Score Master",   Text = "Score 5,000 in one game",   Stat = Stat.BestGameScore, Target = 5000, Reward = 150 },
            new Achievement { Name = "Star Collector", Text = "Earn 30 level stars",       Stat = Stat.LevelStars,    Target = 30,   Reward = 100 },
            new Achievement { Name = "AI Slayer",      Text = "Beat the Master AI",        Stat = Stat.HardAiWins,    Target = 1,    Reward = 150 },
            new Achievement { Name = "Loyal Player",   Text = "Reach a 7-day streak",      Stat = Stat.BestStreak,    Target = 7,    Reward = 100 },
        };

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }          // progress inside the current level
        public readonly int[] Stats = new int[7];
        public int AchievementMask { get; private set; }

        /// <summary>XP needed to go from <paramref name="level"/> to the next: 400, 500, 600 …</summary>
        public static int XpToNext(int level) => 300 + 100 * level;

        /// <summary>Coins for reaching <paramref name="level"/>: 40, 50 … capped at 150.</summary>
        public static int LevelReward(int level) => Math.Min(150, 20 + 10 * level);

        /// <summary>Adds XP (one per point scored). Returns how many levels were gained; coins are granted.</summary>
        public int AddXp(int amount)
        {
            if (amount <= 0) return 0;
            Xp += amount;
            int gained = 0;
            while (Xp >= XpToNext(Level))
            {
                Xp -= XpToNext(Level);
                Level++;
                gained++;
                Coins += LevelReward(Level);
            }
            return gained;
        }

        public bool HasAchievement(int i) => (AchievementMask & (1 << i)) != 0;

        /// <summary>Updates a lifetime stat and returns achievements unlocked by it (their coins are granted).</summary>
        public List<Achievement> Record(Stat stat, int value)
        {
            bool keepsBest = stat == Stat.BestCombo || stat == Stat.BestGameScore || stat == Stat.LevelStars || stat == Stat.BestStreak;
            int k = (int)stat;
            Stats[k] = keepsBest ? Math.Max(Stats[k], value) : Stats[k] + Math.Max(0, value);
            var unlocked = new List<Achievement>();
            for (int i = 0; i < Achievements.Length; i++)
            {
                var a = Achievements[i];
                if (a.Stat != stat || HasAchievement(i) || Stats[k] < a.Target) continue;
                AchievementMask |= 1 << i;
                Coins += a.Reward;
                unlocked.Add(a);
            }
            return unlocked;
        }

        public static string Day(DateTime localDate) => localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>Three missions for a day, the same for everyone on that date.</summary>
        public static Mission[] MissionsFor(string day)
        {
            int seed = 17;
            foreach (char c in day) seed = seed * 31 + c;
            var rng = new Random(seed);
            var picks = new List<int> { 0, 1, 2, 3, 4, 5 };
            var result = new Mission[3];
            for (int i = 0; i < 3; i++)
            {
                int k = rng.Next(picks.Count);
                var (kind, targets, reward) = Pool[picks[k]];
                picks.RemoveAt(k);
                int tier = rng.Next(targets.Length);
                result[i] = new Mission { Kind = kind, Target = targets[tier], Reward = reward + tier * 15 };
            }
            return result;
        }

        /// <summary>Daily login: keeps the streak on consecutive days, resets after a missed day.
        /// Returns coins granted (0 if already checked in today).</summary>
        public int CheckIn(string today)
        {
            if (today == LastCheckIn) return 0;
            bool consecutive = DateTime.TryParseExact(LastCheckIn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last)
                && DateTime.ParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture) == last.AddDays(1);
            Streak = consecutive ? Streak + 1 : 1;
            BestStreak = Math.Max(BestStreak, Streak);
            LastCheckIn = today;
            int reward = StreakReward(Streak);
            Coins += reward;
            return reward;
        }

        /// <summary>10, 15, 20 … growing each day, capped at 60, with a bonus every 7th day.</summary>
        public static int StreakReward(int streakDay) => Math.Min(60, 5 + streakDay * 5) + (streakDay % 7 == 0 ? 100 : 0);

        private void RollMissions(string today)
        {
            if (MissionDay == today) return;
            MissionDay = today;
            Array.Clear(Progress, 0, 3);
            Array.Clear(Claimed, 0, 3);
        }

        /// <summary>Records progress. ComboReach tracks the best value instead of a sum.</summary>
        public void Track(MissionKind kind, int amount, string today)
        {
            RollMissions(today);
            var missions = MissionsFor(today);
            for (int i = 0; i < 3; i++)
            {
                if (missions[i].Kind != kind) continue;
                Progress[i] = kind == MissionKind.ComboReach ? Math.Max(Progress[i], amount) : Progress[i] + amount;
            }
        }

        public bool IsComplete(int i, string today)
        {
            RollMissions(today);
            return Progress[i] >= MissionsFor(today)[i].Target;
        }

        public bool Claim(int i, string today)
        {
            if (!IsComplete(i, today) || Claimed[i]) return false;
            Claimed[i] = true;
            Coins += MissionsFor(today)[i].Reward;
            return true;
        }

        public bool Owns(int theme) => (OwnedThemes & (1 << theme)) != 0;

        public bool BuyOrSelect(int theme)
        {
            if (theme < 0 || theme >= Themes.Length) return false;
            if (!Owns(theme))
            {
                if (Coins < Themes[theme].Price) return false;
                Coins -= Themes[theme].Price;
                OwnedThemes |= 1 << theme;
            }
            ThemeIndex = theme;
            return true;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append($"c={Coins};s={Streak};b={BestStreak};l={LastCheckIn};m={MissionDay};o={OwnedThemes};t={ThemeIndex};");
            sb.Append($"p={Progress[0]},{Progress[1]},{Progress[2]};k={(Claimed[0] ? 1 : 0)}{(Claimed[1] ? 1 : 0)}{(Claimed[2] ? 1 : 0)};");
            sb.Append($"v={Level};x={Xp};a={AchievementMask};st={string.Join(",", Stats)}");
            return sb.ToString();
        }

        public static PlayerMeta Parse(string data)
        {
            var m = new PlayerMeta();
            if (string.IsNullOrEmpty(data)) return m;
            foreach (var part in data.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                int.TryParse(v, out int n);
                switch (k)
                {
                    case "c": m.Coins = Math.Max(0, n); break;
                    case "s": m.Streak = Math.Max(0, n); break;
                    case "b": m.BestStreak = Math.Max(0, n); break;
                    case "l": m.LastCheckIn = v; break;
                    case "m": m.MissionDay = v; break;
                    case "o": m.OwnedThemes = n | 1; break;
                    case "t": m.ThemeIndex = n >= 0 && n < Themes.Length ? n : 0; break;
                    case "p":
                        var ps = v.Split(',');
                        for (int i = 0; i < 3 && i < ps.Length; i++) int.TryParse(ps[i], out m.Progress[i]);
                        break;
                    case "k":
                        for (int i = 0; i < 3 && i < v.Length; i++) m.Claimed[i] = v[i] == '1';
                        break;
                    case "v": m.Level = Math.Max(1, n); break;
                    case "x": m.Xp = Math.Max(0, n); break;
                    case "a": m.AchievementMask = n; break;
                    case "st":
                        var ss = v.Split(',');
                        for (int i = 0; i < m.Stats.Length && i < ss.Length; i++) int.TryParse(ss[i], out m.Stats[i]);
                        break;
                }
            }
            if (!m.Owns(m.ThemeIndex)) m.ThemeIndex = 0;
            return m;
        }
    }
}
