using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlockDrop.Core
{
    public enum MissionKind { ClearLines, ScorePoints, PlaceBlocks, ComboReach, PlayGames, EarnStars, PlayDaily, BeatAi }

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
            MissionKind.PlayDaily => Loc.L("Play today's Daily challenge"),
            MissionKind.BeatAi => Target == 1 ? Loc.L("Beat the AI once") : Loc.F("Beat the AI {0} times", Target),
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

    /// <summary>Starter themes are cheap, premium ones are a goal for a few weeks of play, legendary ones are the
    /// long-term prize (or a direct purchase). Exclusive themes cannot be bought at all: they are earned, so they
    /// say something about the player.</summary>
    public enum ThemeTier { Starter, Premium, Legendary, Exclusive }

    /// <summary>Level helpers. Earned in play (a starter gift, every chapter, every 7th streak day) or bought with
    /// coins. They give the player a choice at the hardest moment instead of a wall.</summary>
    public enum Booster { Hammer, Shuffle }

    public sealed class Theme
    {
        public string Name;
        public int Price;
        public string[] Blocks;   // 5 hex colours
        public string BackgroundTint;
        public ThemeTier Tier;
        public string Panel = "101845", Slot = "1E2A6B"; // board and empty cell colours
        public int ChapterReward; // exclusive: earned by finishing this chapter
        public int StarsReward;   // exclusive: earned with this many level stars
        public string Tagline = "";
        public bool Buyable => Tier != ThemeTier.Exclusive;
    }

    /// <summary>Fair, skill-based progression: daily streak, three daily missions, coins and cosmetic
    /// themes. No randomised paid rewards. Pure logic (dates are passed in) so it is fully testable.</summary>
    public sealed class PlayerMeta
    {
        public static readonly Theme[] Themes =
        {
            new Theme { Name = "Jewel",  Price = 0,   Blocks = new[] { "FF5C7A", "FFC83D", "3DF2AA", "42D2FF", "B57DFF" }, BackgroundTint = "FFFFFF" },
            new Theme { Name = "Neon",   Price = 150, Blocks = new[] { "FF2E93", "F9F871", "00F5A0", "00D9FF", "B967FF" }, BackgroundTint = "6B5BA8" },
            new Theme { Name = "Candy",  Price = 250, Blocks = new[] { "FF8FAB", "FFD6A5", "9BF6C4", "A0E7FF", "CDB4FF" }, BackgroundTint = "FFC2E2" },
            new Theme { Name = "Ocean",  Price = 350, Blocks = new[] { "00B4D8", "48CAE4", "2EC4B6", "0077B6", "90E0EF" }, BackgroundTint = "8FD3FF" },
            new Theme { Name = "Sunset", Price = 500, Blocks = new[] { "FF6B35", "F7C59F", "EF476F", "FFD166", "C77DFF" }, BackgroundTint = "FFB38A" },
            // premium: a board that changes too, not only the blocks
            new Theme { Name = "Forest", Price = 1200, Tier = ThemeTier.Premium, Blocks = new[] { "7BC950", "E9C46A", "2A9D8F", "F4A261", "B5E48C" }, BackgroundTint = "6FBF73", Panel = "14301F", Slot = "21482F", Tagline = "Calm greens and warm wood" },
            new Theme { Name = "Arcade", Price = 1500, Tier = ThemeTier.Premium, Blocks = new[] { "FF006E", "FFBE0B", "3A86FF", "8338EC", "06FFA5" }, BackgroundTint = "3B2A6E", Panel = "0B0B1A", Slot = "1A1A33", Tagline = "Neon on midnight black" },
            new Theme { Name = "Sakura", Price = 1800, Tier = ThemeTier.Premium, Blocks = new[] { "FFB7C5", "FF8FAB", "FFE5EC", "C9ADA7", "F2A7C3" }, BackgroundTint = "FFD1DC", Panel = "4A1E33", Slot = "6B2E4B", Tagline = "Spring blossom pinks" },
            new Theme { Name = "Midnight", Price = 2200, Tier = ThemeTier.Premium, Blocks = new[] { "4CC9F0", "4361EE", "B8C0FF", "F72585", "7209B7" }, BackgroundTint = "283593", Panel = "0A0F2C", Slot = "141B45", Tagline = "City lights after dark" },
            new Theme { Name = "Desert", Price = 2500, Tier = ThemeTier.Premium, Blocks = new[] { "E76F51", "F4A261", "E9C46A", "2A9D8F", "D4A373" }, BackgroundTint = "E8B97A", Panel = "3D2416", Slot = "5A3824", Tagline = "Terracotta and gold sand" },
            // legendary: the long-term prize
            new Theme { Name = "Aurora", Price = 5000, Tier = ThemeTier.Legendary, Blocks = new[] { "00F5D4", "00BBF9", "9B5DE5", "F15BB5", "FEE440" }, BackgroundTint = "1B4D5C", Panel = "06131F", Slot = "0F2433", Tagline = "Northern lights on every block" },
            new Theme { Name = "Galaxy", Price = 6500, Tier = ThemeTier.Legendary, Blocks = new[] { "C77DFF", "7B2CBF", "E0AAFF", "48BFE3", "FF70A6" }, BackgroundTint = "2D1B69", Panel = "0D0221", Slot = "1E0B45", Tagline = "Deep space violet" },
            new Theme { Name = "Diamond", Price = 8000, Tier = ThemeTier.Legendary, Blocks = new[] { "E0FBFC", "B9E6FF", "FFFFFF", "C2DFE3", "9DB4C0" }, BackgroundTint = "AFC8D6", Panel = "1D2D44", Slot = "2E4460", Tagline = "Ice-clear luxury" },
            new Theme { Name = "Royal Gold", Price = 9000, Tier = ThemeTier.Legendary, Blocks = new[] { "FFD700", "FFC300", "F5E6A8", "D4AF37", "FFF1B5" }, BackgroundTint = "4A2368", Panel = "1F0B33", Slot = "35154F", Tagline = "Solid gold on royal purple" },
            // exclusive: earned, never sold
            new Theme { Name = "Glacier", Tier = ThemeTier.Exclusive, ChapterReward = 5, Blocks = new[] { "A9E4FF", "6EC1E4", "DFF6FF", "3E92CC", "B8F2E6" }, BackgroundTint = "7FB7D9", Panel = "0E2A3D", Slot = "173F5A", Tagline = "Finish chapter 5" },
            new Theme { Name = "Crown Jewels", Tier = ThemeTier.Exclusive, ChapterReward = 10, Blocks = new[] { "E0115F", "0F52BA", "50C878", "FFD700", "9966CC" }, BackgroundTint = "3D155F", Panel = "140626", Slot = "2A0F45", Tagline = "Finish all 100 levels" },
            new Theme { Name = "Starlight", Tier = ThemeTier.Exclusive, StarsReward = 250, Blocks = new[] { "FFF3B0", "FFD6FF", "BDE0FE", "CAFFBF", "FFC6FF" }, BackgroundTint = "22223B", Panel = "0B0B1E", Slot = "1A1A38", Tagline = "Earn 250 level stars" },
        };

        private static readonly (MissionKind kind, int[] targets, int reward)[] Pool =
        {
            (MissionKind.ClearLines, new[] { 10, 20, 30 }, 30),
            (MissionKind.ScorePoints, new[] { 500, 1000, 2000 }, 30),
            (MissionKind.PlaceBlocks, new[] { 30, 50, 80 }, 20),
            (MissionKind.ComboReach, new[] { 2, 3, 4 }, 40),
            (MissionKind.PlayGames, new[] { 2, 3, 5 }, 25),
            (MissionKind.EarnStars, new[] { 2, 3, 5 }, 35),
            (MissionKind.PlayDaily, new[] { 1 }, 40),
            (MissionKind.BeatAi, new[] { 1, 2, 3 }, 40),
        };

        public int Coins { get; private set; }

        public const int StarterBoosters = 3, ChapterBoosters = 2;
        public readonly int[] Boosters = { StarterBoosters, StarterBoosters };
        public static int BoosterPrice(Booster b) => b == Booster.Hammer ? 150 : 100;
        public int BoostersOf(Booster b) => Boosters[(int)b];
        public void AddBoosters(Booster b, int n) { if (n > 0) Boosters[(int)b] += n; }

        public bool UseBooster(Booster b)
        {
            if (Boosters[(int)b] <= 0) return false;
            Boosters[(int)b]--;
            return true;
        }

        public bool BuyBooster(Booster b)
        {
            if (!Spend(BoosterPrice(b))) return false;
            Boosters[(int)b]++;
            return true;
        }
        public void AddCoins(int amount) { if (amount > 0) Coins += amount; }

        /// <summary>Pays <paramref name="amount"/> coins if the player has them.</summary>
        public bool Spend(int amount)
        {
            if (amount < 0 || Coins < amount) return false;
            Coins -= amount;
            return true;
        }
        public int Streak { get; private set; }

        /// <summary>Streak shields: one is spent automatically when exactly one day was missed.</summary>
        public const int MaxStreakFreezes = 2, StreakFreezePrice = 120;
        public int StreakFreezes { get; private set; }
        /// <summary>True when the last <see cref="CheckIn"/> was kept alive by a shield.</summary>
        public bool FreezeUsedToday { get; private set; }
        public bool BuyStreakFreeze()
        {
            if (StreakFreezes >= MaxStreakFreezes || !Spend(StreakFreezePrice)) return false;
            StreakFreezes++;
            return true;
        }
        public int BestStreak { get; private set; }
        public string LastCheckIn { get; private set; } = "";
        public string MissionDay { get; private set; } = "";
        public readonly int[] Progress = new int[3];
        public readonly bool[] Claimed = new bool[3];
        /// <summary>Today's chest: opens once all three missions are claimed.</summary>
        public bool ChestClaimed;
        public const int ChestCoins = 50;
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

        // ---- mode ranks (Classic, Daily, VS AI) ----
        public readonly int[] RankPoints = new int[3];
        /// <summary>Highest step ever reached per track, so a trophy dip and climb never pays a reward twice.</summary>
        public readonly int[] RankPeak = new int[3];
        public string DailyRankDay { get; private set; } = "";
        public int AiWinStreak { get; private set; }

        public int RankStep(RankTrack t) => Ranks.StepOf(t, RankPoints[(int)t]);

        /// <summary>Applies a finished game to a ladder and pays any rank-up rewards. Classic and Daily only add points.</summary>
        public RankResult AwardRank(RankTrack t, int delta)
        {
            int k = (int)t, before = RankPoints[k];
            var r = new RankResult { Track = t, Before = before, StepBefore = Ranks.StepOf(t, before) };
            RankPoints[k] = Math.Max(0, before + delta);
            r.After = RankPoints[k];
            r.Delta = r.After - before;
            r.StepAfter = Ranks.StepOf(t, r.After);
            for (int s = RankPeak[k] + 1; s <= r.StepAfter; s++)
            {
                r.Coins += Ranks.StepCoins(t, s);
                if (Ranks.TierStart(s)) { r.Hammers++; r.Shuffles++; }
            }
            RankPeak[k] = Math.Max(RankPeak[k], r.StepAfter);
            Coins += r.Coins;
            AddBoosters(Booster.Hammer, r.Hammers);
            AddBoosters(Booster.Shuffle, r.Shuffles);
            return r;
        }

        public RankResult RankClassic(int score) => AwardRank(RankTrack.Classic, Ranks.ClassicGain(score));

        /// <summary>Only the first Daily game of a day counts, so replaying the same seed can't be farmed.</summary>
        public RankResult RankDaily(int score, string today)
        {
            if (DailyRankDay == today)
            {
                int p = RankPoints[(int)RankTrack.Daily], s = Ranks.StepOf(RankTrack.Daily, p);
                return new RankResult { Track = RankTrack.Daily, Before = p, After = p, StepBefore = s, StepAfter = s, AlreadyScored = true };
            }
            DailyRankDay = today;
            return AwardRank(RankTrack.Daily, Ranks.DailyGain(score, Streak));
        }

        public RankResult RankVs(BotLevel level, int winner)
        {
            int delta = Ranks.VsDelta(level, winner, AiWinStreak, RankPoints[(int)RankTrack.VsAi], out bool capped);
            AiWinStreak = winner == 0 ? AiWinStreak + 1 : winner > 0 ? 0 : AiWinStreak;
            var r = AwardRank(RankTrack.VsAi, delta);
            r.Capped = capped;
            return r;
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
            var picks = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
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
            FreezeUsedToday = false;
            bool hasLast = DateTime.TryParseExact(LastCheckIn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last);
            var now = DateTime.ParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            bool consecutive = hasLast && now == last.AddDays(1);
            if (!consecutive && hasLast && now == last.AddDays(2) && Streak > 0 && StreakFreezes > 0)
            {
                StreakFreezes--; // the missed day is covered
                FreezeUsedToday = consecutive = true;
            }
            Streak = consecutive ? Streak + 1 : 1;
            BestStreak = Math.Max(BestStreak, Streak);
            LastCheckIn = today;
            if (Streak % 7 == 0) { AddBoosters(Booster.Hammer, 1); StreakFreezes = Math.Min(MaxStreakFreezes, StreakFreezes + 1); } // a weekly treat on top of the coins
            int reward = StreakReward(Streak);
            Coins += reward;
            return reward;
        }

        /// <summary>Which of the 7 steps of the weekly ladder a streak day lands on (1-7).</summary>
        public static int LadderDay(int streakDay) => (Math.Max(1, streakDay) - 1) % 7 + 1;

        /// <summary>10, 15, 20 … growing each day, capped at 60, with a bonus every 7th day.</summary>
        public static int StreakReward(int streakDay) => Math.Min(60, 5 + streakDay * 5) + (streakDay % 7 == 0 ? 100 : 0);

        private void RollMissions(string today)
        {
            if (MissionDay == today) return;
            MissionDay = today;
            Array.Clear(Progress, 0, 3);
            Array.Clear(Claimed, 0, 3);
            ChestClaimed = false;
        }

        public int ClaimedCount(string today) { RollMissions(today); return (Claimed[0] ? 1 : 0) + (Claimed[1] ? 1 : 0) + (Claimed[2] ? 1 : 0); }
        public bool ChestReady(string today) => ClaimedCount(today) == 3 && !ChestClaimed;

        /// <summary>Finishing all three missions always pays the same chest: coins, a hammer and a shuffle.</summary>
        public bool ClaimChest(string today)
        {
            if (!ChestReady(today)) return false;
            ChestClaimed = true;
            Coins += ChestCoins;
            AddBoosters(Booster.Hammer, 1); AddBoosters(Booster.Shuffle, 1);
            return true;
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
                if (!Themes[theme].Buyable) return false;
                if (Coins < Themes[theme].Price) return false;
                Coins -= Themes[theme].Price;
                OwnedThemes |= 1 << theme;
            }
            ThemeIndex = theme;
            return true;
        }

        /// <summary>Exclusive themes earned by finishing chapters or collecting stars. Returns newly unlocked indexes.</summary>
        public List<int> UnlockEarnedThemes(int chaptersComplete, int totalStars)
        {
            var got = new List<int>();
            for (int i = 0; i < Themes.Length; i++)
            {
                var t = Themes[i];
                if (t.Tier != ThemeTier.Exclusive || Owns(i)) continue;
                bool earned = (t.ChapterReward > 0 && chaptersComplete >= t.ChapterReward) || (t.StarsReward > 0 && totalStars >= t.StarsReward);
                if (!earned) continue;
                OwnedThemes |= 1 << i;
                got.Add(i);
            }
            return got;
        }

        public static int FindTheme(string name) => Array.FindIndex(Themes, t => t.Name == name);

        /// <summary>Non-consumable store products already granted (bit per <see cref="Store.Products"/> index).</summary>
        public int Purchases { get; private set; }
        public bool NoAds { get; private set; }

        /// <summary>Delivers a confirmed purchase. Consumables (coin packs) stack; a non-consumable is granted once,
        /// so a restore on a new phone never pays out twice.</summary>
        public bool Grant(Product product)
        {
            int i = Array.IndexOf(Store.Products, product);
            if (i < 0) return false;
            if (!product.Consumable)
            {
                if ((Purchases & (1 << i)) != 0) return false;
                Purchases |= 1 << i;
            }
            Coins += product.Coins;
            foreach (var name in product.Themes)
            {
                int t = FindTheme(name);
                if (t >= 0) OwnedThemes |= 1 << t;
            }
            if (product.RemoveAds) NoAds = true;
            AddBoosters(Booster.Hammer, product.Hammers);
            AddBoosters(Booster.Shuffle, product.Shuffles);
            return true;
        }

        /// <summary>Combines two records of the same player (this phone and the cloud copy) without losing anything earned.
        /// The record further along (level, then xp, then coins) is the base; themes, achievements, best streak and
        /// lifetime stats keep the larger/union of both.</summary>
        public static PlayerMeta Merge(PlayerMeta a, PlayerMeta b)
        {
            bool aLeads = a.Level != b.Level ? a.Level > b.Level : a.Xp != b.Xp ? a.Xp > b.Xp : a.Coins >= b.Coins;
            var lead = aLeads ? a : b;
            var other = aLeads ? b : a;
            var m = Parse(lead.Serialize());
            m.OwnedThemes = lead.OwnedThemes | other.OwnedThemes;
            m.AchievementMask = lead.AchievementMask | other.AchievementMask;
            m.BestStreak = Math.Max(lead.BestStreak, other.BestStreak);
            m.StreakFreezes = Math.Max(lead.StreakFreezes, other.StreakFreezes);
            for (int i = 0; i < m.Stats.Length; i++) m.Stats[i] = Math.Max(lead.Stats[i], other.Stats[i]);
            m.Purchases = lead.Purchases | other.Purchases;
            for (int i = 0; i < m.RankPoints.Length; i++)
            {
                m.RankPoints[i] = Math.Max(lead.RankPoints[i], other.RankPoints[i]);
                m.RankPeak[i] = Math.Max(Math.Max(lead.RankPeak[i], other.RankPeak[i]), Ranks.StepOf((RankTrack)i, m.RankPoints[i]));
            }
            if (string.CompareOrdinal(other.DailyRankDay, lead.DailyRankDay) > 0) m.DailyRankDay = other.DailyRankDay;
            m.NoAds = lead.NoAds || other.NoAds;
            if (lead.MissionDay == other.MissionDay) m.ChestClaimed = lead.ChestClaimed || other.ChestClaimed; // a chest opened on one device stays opened on both
            return m;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append($"c={Coins};s={Streak};b={BestStreak};l={LastCheckIn};m={MissionDay};o={OwnedThemes};t={ThemeIndex};");
            sb.Append($"p={Progress[0]},{Progress[1]},{Progress[2]};k={(Claimed[0] ? 1 : 0)}{(Claimed[1] ? 1 : 0)}{(Claimed[2] ? 1 : 0)};");
            sb.Append($"v={Level};x={Xp};a={AchievementMask};st={string.Join(",", Stats)};pu={Purchases};na={(NoAds ? 1 : 0)};dc={(ChestClaimed ? 1 : 0)};rp={string.Join(",", RankPoints)};rk={string.Join(",", RankPeak)};dd={DailyRankDay};ws={AiWinStreak};bo={Boosters[0]},{Boosters[1]}");
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
                    case "dc": m.ChestClaimed = v == "1"; break;
                    case "v": m.Level = Math.Max(1, n); break;
                    case "x": m.Xp = Math.Max(0, n); break;
                    case "a": m.AchievementMask = n; break;
                    case "pu": m.Purchases = n; break;
                    case "bo":
                        var bs = v.Split(',');
                        for (int i = 0; i < m.Boosters.Length && i < bs.Length; i++)
                            if (int.TryParse(bs[i], out int count)) m.Boosters[i] = Math.Max(0, count);
                        break;
                    case "rp": ParseInts(v, m.RankPoints); break;
                    case "rk": ParseInts(v, m.RankPeak); break;
                    case "dd": m.DailyRankDay = v; break;
                    case "ws": m.AiWinStreak = Math.Max(0, n); break;
                    case "na": m.NoAds = n == 1; break;
                    case "st":
                        var ss = v.Split(',');
                        for (int i = 0; i < m.Stats.Length && i < ss.Length; i++) int.TryParse(ss[i], out m.Stats[i]);
                        break;
                }
            }
            if (!m.Owns(m.ThemeIndex)) m.ThemeIndex = 0;
            for (int i = 0; i < m.RankPoints.Length; i++)
            {
                m.RankPoints[i] = Math.Max(0, m.RankPoints[i]);
                m.RankPeak[i] = Math.Max(Math.Max(0, m.RankPeak[i]), Ranks.StepOf((RankTrack)i, m.RankPoints[i]));
            }
            return m;
        }

        private static void ParseInts(string v, int[] into)
        {
            var parts = v.Split(',');
            for (int i = 0; i < into.Length && i < parts.Length; i++) int.TryParse(parts[i], out into[i]);
        }
    }
}
