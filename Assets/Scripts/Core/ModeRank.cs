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

    /// <summary>One ladder shape for all three modes: Bronze, Silver, Gold, Diamond and Crown each have five
    /// divisions (V up to I) that split the rank's point range evenly, then Master, Champion and Legend stand alone.
    /// King (top 100 players) is not here on purpose: it needs a server ranking, see CloudCode/RANKS.md.
    /// Classic and Daily only ever go up; VS AI can drop, but never below the start of the current rank.</summary>
    public static class Ranks
    {
        public static readonly string[] Tiers = { "Bronze", "Silver", "Gold", "Diamond", "Crown", "Master", "Champion", "Legend" };
        static readonly string[] Divisions = { "V", "IV", "III", "II", "I" };
        static readonly string[] TierColors = { "D98B4F", "C7D2E8", "FFD54A", "6FC8FF", "FF9BD2", "C77DFF", "5FE0D0", "FF6B8B" };
        /// <summary>Points where each rank begins.</summary>
        static readonly int[] TierStarts = { 0, 600, 1500, 2800, 4500, 6500, 8500, 11000 };

        public const int DivisionsPerTier = 5;
        public const int DividedTiers = 5;                       // Bronze to Crown
        public const int TopStep = DividedTiers * DivisionsPerTier + 2; // Master, Champion, Legend follow
        public const int EasyCap = 600, MediumCap = 2800;        // the easier AIs stop paying beyond this

        public static int PointsFor(RankTrack t, int step)
        {
            if (step <= 0) return 0;
            if (step > TopStep) step = TopStep;
            int d = DividedTiers * DivisionsPerTier;
            if (step >= d) return TierStarts[DividedTiers + (step - d)];
            int r = step / DivisionsPerTier, i = step % DivisionsPerTier;
            return TierStarts[r] + (TierStarts[r + 1] - TierStarts[r]) * i / DivisionsPerTier;
        }

        public static int StepOf(RankTrack t, int points)
        {
            int step = 0;
            while (step < TopStep && points >= PointsFor(t, step + 1)) step++;
            return step;
        }

        public static int TierOf(int step) => step < DividedTiers * DivisionsPerTier ? step / DivisionsPerTier : DividedTiers + (step - DividedTiers * DivisionsPerTier);
        static int FirstStepOf(int tier) => tier < DividedTiers ? tier * DivisionsPerTier : DividedTiers * DivisionsPerTier + (tier - DividedTiers);
        public static string TierName(int step) => Tiers[TierOf(step)];
        public static string TierColor(int step) => TierColors[TierOf(step)];

        /// <summary>"GOLD II"; Master, Champion and Legend have no division.</summary>
        public static string Name(int step) =>
            (TierOf(step) < DividedTiers ? Tiers[TierOf(step)] + " " + Divisions[step % DivisionsPerTier] : Tiers[TierOf(step)]).ToUpperInvariant();

        /// <summary>Same as <see cref="Name"/> in the player's language ("ORO II"); Name stays English for logic and tests.</summary>
        public static string Display(int step)
        {
            string tier = Loc.L(Tiers[TierOf(step)].ToUpperInvariant());
            return TierOf(step) < DividedTiers ? tier + " " + Divisions[step % DivisionsPerTier] : tier;
        }

        /// <summary>0..1 progress from this step to the next; 1 at the top.</summary>
        public static float Fill(RankTrack t, int points)
        {
            int s = StepOf(t, points);
            if (s >= TopStep) return 1f;
            int a = PointsFor(t, s), b = PointsFor(t, s + 1);
            return Math.Clamp((points - a) / (float)(b - a), 0f, 1f);
        }

        public static int ToNext(RankTrack t, int points) { int s = StepOf(t, points); return s >= TopStep ? 0 : PointsFor(t, s + 1) - points; }

        /// <summary>Lowest count the player can fall to: the start of their current rank (Bronze can't fall).</summary>
        public static int Floor(RankTrack t, int points) => PointsFor(t, FirstStepOf(TierOf(StepOf(t, points))));

        // ---- how a game turns into points ----

        public static int ClassicGain(int score) => 15 + Math.Min(135, Math.Max(0, score) / 30);

        public static int DailyGain(int score, int loginStreak) => 50 + Math.Min(100, Math.Max(0, score) / 40) + 5 * Math.Min(10, Math.Max(0, loginStreak));

        /// <summary>Trophies for a VS AI result. Harder AIs pay more and cost less; Easy and Medium stop paying once
        /// you are past their cap (a loss still costs, so they can't be farmed). A win streak adds up to +10.</summary>
        public static int VsDelta(BotLevel level, int winner, int winStreak, int trophies, out bool capped)
        {
            capped = false;
            if (winner < 0) return 0;
            int win = level == BotLevel.Easy ? 12 : level == BotLevel.Medium ? 25 : 40;
            int loss = level == BotLevel.Easy ? 15 : level == BotLevel.Medium ? 12 : 8;
            if (winner != 0) return -Math.Min(loss, trophies - Floor(RankTrack.VsAi, trophies));
            int cap = level == BotLevel.Easy ? EasyCap : level == BotLevel.Medium ? MediumCap : int.MaxValue;
            if (trophies >= cap) { capped = true; return 0; }
            return win + Math.Min(10, 2 * Math.Max(0, winStreak));
        }

        // ---- one-time rewards for reaching a step ----

        public static int StepCoins(RankTrack t, int step) => (25 + 5 * step) * (t == RankTrack.VsAi ? 3 : 2) / 2 + (step >= TopStep ? 500 : 0);

        /// <summary>A new tier also brings a hammer and a shuffle: a real gift, not just coins.</summary>
        public static bool TierStart(int step) => step > 0 && (step >= DividedTiers * DivisionsPerTier || step % DivisionsPerTier == 0);
    }
}
