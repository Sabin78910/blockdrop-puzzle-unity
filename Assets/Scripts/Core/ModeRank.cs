using System;

namespace BlockDrop.Core
{
    /// <summary>The three open-ended modes each get their own ladder, so there is always a next rank to reach.</summary>
    public enum RankTrack { Classic, Daily, VsAi }

    /// <summary>What one game did to a ladder; the result screen animates from <see cref="Before"/> to <see cref="After"/>.</summary>
    public sealed class RankResult
    {
        public RankTrack Track;
        public int Before, After, Delta;
        public int StepBefore, StepAfter;
        public int Coins, Hammers, Shuffles;
        public bool Promoted => StepAfter > StepBefore;
        public bool Demoted => StepAfter < StepBefore;
        /// <summary>Easy or Medium AI win that earned nothing because the player outgrew that opponent.</summary>
        public bool Capped;
        /// <summary>Daily only: today's attempt already counted.</summary>
        public bool AlreadyScored;
    }

    /// <summary>Rank ladders: Rookie to Legend, three divisions per tier. Classic and Daily only ever go up (skill
    /// shown, never punished); VS AI is a trophy count that can drop, but never below the start of the current tier.</summary>
    public static class Ranks
    {
        public static readonly string[] Tiers = { "Rookie", "Bronze", "Silver", "Gold", "Platinum", "Diamond", "Master", "Legend" };
        static readonly string[] Divisions = { "III", "II", "I" };
        static readonly string[] TierColors = { "9AA3C7", "D98B4F", "C7D2E8", "FFD54A", "5FE0D0", "6FC8FF", "C77DFF", "FF6B8B" };

        public const int DivisionsPerTier = 3;
        public const int TopStep = 7 * DivisionsPerTier; // 7 tiers with divisions, then Legend
        public const int EasyCap = 150, MediumCap = 600; // trophies the easier AIs stop paying beyond

        static int Unit(RankTrack t) => t == RankTrack.Classic ? 30 : t == RankTrack.Daily ? 20 : 30;

        /// <summary>Points needed to stand on <paramref name="step"/>: 1, 3, 6, 10 … times the track's unit.</summary>
        public static int PointsFor(RankTrack t, int step) => step <= 0 ? 0 : Unit(t) * step * (step + 1) / 2;

        public static int StepOf(RankTrack t, int points)
        {
            int step = 0;
            while (step < TopStep && points >= PointsFor(t, step + 1)) step++;
            return step;
        }

        public static int TierOf(int step) => Math.Min(step / DivisionsPerTier, Tiers.Length - 1);
        public static string TierName(int step) => Tiers[TierOf(step)];
        public static string TierColor(int step) => TierColors[TierOf(step)];

        /// <summary>"GOLD II"; Legend has no division.</summary>
        public static string Name(int step) =>
            (step >= TopStep ? Tiers[Tiers.Length - 1] : Tiers[TierOf(step)] + " " + Divisions[step % DivisionsPerTier]).ToUpperInvariant();

        /// <summary>0..1 progress from this step to the next; 1 at the top.</summary>
        public static float Fill(RankTrack t, int points)
        {
            int s = StepOf(t, points);
            if (s >= TopStep) return 1f;
            int a = PointsFor(t, s), b = PointsFor(t, s + 1);
            return Math.Clamp((points - a) / (float)(b - a), 0f, 1f);
        }

        public static int ToNext(RankTrack t, int points) { int s = StepOf(t, points); return s >= TopStep ? 0 : PointsFor(t, s + 1) - points; }

        /// <summary>Lowest trophy count the player can fall to: the start of their current tier (Rookie can't fall).</summary>
        public static int Floor(RankTrack t, int points) => PointsFor(t, TierOf(StepOf(t, points)) * DivisionsPerTier);

        // ---- how a game turns into points ----

        public static int ClassicGain(int score) => 10 + Math.Min(140, Math.Max(0, score) / 40);

        public static int DailyGain(int score, int loginStreak) => 30 + Math.Min(100, Math.Max(0, score) / 50) + 5 * Math.Min(10, Math.Max(0, loginStreak));

        /// <summary>Trophies for a VS AI result. Harder AIs pay more and cost less; Easy and Medium stop paying once
        /// you are past their cap (a loss still costs, so they can't be farmed). A win streak adds up to +10.</summary>
        public static int VsDelta(BotLevel level, int winner, int winStreak, int trophies, out bool capped)
        {
            capped = false;
            if (winner < 0) return 0;
            int win = level == BotLevel.Easy ? 8 : level == BotLevel.Medium ? 20 : 32;
            int loss = level == BotLevel.Easy ? 12 : level == BotLevel.Medium ? 10 : 8;
            if (winner != 0) return -Math.Min(loss, trophies - Floor(RankTrack.VsAi, trophies));
            int cap = level == BotLevel.Easy ? EasyCap : level == BotLevel.Medium ? MediumCap : int.MaxValue;
            if (trophies >= cap) { capped = true; return 0; }
            return win + Math.Min(10, 2 * Math.Max(0, winStreak));
        }

        // ---- one-time rewards for reaching a step ----

        public static int StepCoins(RankTrack t, int step) => (25 + 5 * step) * (t == RankTrack.VsAi ? 3 : 2) / 2 + (step >= TopStep ? 500 : 0);

        /// <summary>A new tier also brings a hammer and a shuffle: a real gift, not just coins.</summary>
        public static bool TierStart(int step) => step > 0 && (step % DivisionsPerTier == 0 || step >= TopStep);
    }
}
