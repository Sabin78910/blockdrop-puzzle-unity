using System;
using System.Collections.Generic;

namespace BlockDrop.Core
{
    /// <summary>How big a clear's celebration is. Feedback scales with what the player achieved:
    /// research found amplified feedback for small events lowers competence, while feedback tied to real
    /// success raises it (docs/FEEL.md, design spec "Feedback ladder").</summary>
    public enum FeedbackTier { None, Line, Great, Excellent, Amazing, BoardClear }

    public static class Feel
    {
        /// <param name="lines">Lines cleared by this placement.</param>
        /// <param name="combo">The board's combo after the placement (1 = first clear in a row).</param>
        /// <param name="boardEmpty">True when the placement left the board empty.</param>
        public static FeedbackTier TierFor(int lines, int combo, bool boardEmpty)
        {
            if (lines <= 0) return FeedbackTier.None;
            if (boardEmpty) return FeedbackTier.BoardClear;
            if (lines >= 4 || combo >= 4) return FeedbackTier.Amazing;
            if (lines == 3) return FeedbackTier.Excellent;
            if (lines == 2 || combo >= 2) return FeedbackTier.Great;
            return FeedbackTier.Line;
        }

        /// <summary>The praise word (English key for <see cref="Loc"/>), or null when the tier gets none.</summary>
        public static string Praise(FeedbackTier tier) => tier switch
        {
            FeedbackTier.Great => "GREAT!",
            FeedbackTier.Excellent => "EXCELLENT!",
            FeedbackTier.Amazing => "AMAZING!",
            FeedbackTier.BoardClear => "PERFECT CLEAR!",
            _ => null,
        };

        /// <summary>Screen shake in seconds. Only 3+ line moments shake, capped, and never under reduced motion.</summary>
        public static float Shake(FeedbackTier tier, int lines, bool reducedMotion)
        {
            if (reducedMotion || tier < FeedbackTier.Excellent) return 0f;
            return Math.Min(0.4f, 0.12f + 0.08f * lines);
        }

        /// <summary>Slow motion is reserved for the biggest moments and is off under reduced motion.</summary>
        public static bool SlowMotion(FeedbackTier tier, bool reducedMotion) => !reducedMotion && tier >= FeedbackTier.Amazing;

        /// <summary>Hit stop (freeze frame) length in seconds; 0 under reduced motion.</summary>
        public static float HitStop(int lines, bool reducedMotion) => reducedMotion || lines <= 0 ? 0f : 0.035f + 0.02f * lines;

        /// <summary>Confetti count. Gold confetti stays for new bests, so only a board clear gets a burst here.</summary>
        public static int Confetti(FeedbackTier tier, bool reducedMotion) => reducedMotion ? 0 : tier == FeedbackTier.BoardClear ? 40 : 0;

        /// <summary>Reduced motion: the player's choice wins (0 full, 1 reduced); with no choice (-1) follow the
        /// phone, where Android's "remove animations" sets the animator duration scale to 0.</summary>
        public static bool ReducedMotion(int playerChoice, float systemAnimatorScale) =>
            playerChoice == 1 || (playerChoice < 0 && systemAnimatorScale <= 0f);

        public static bool IsEmpty(Board b)
        {
            for (int x = 0; x < b.Size; x++) for (int y = 0; y < b.Size; y++) if (b.IsFilled(x, y)) return false;
            return true;
        }
    }

    /// <summary>The new look ships behind a switch. <see cref="NewLookDefault"/> is what players get until they
    /// choose; the release build flips this one constant to turn it on for everyone.</summary>
    public static class Look
    {
        public const bool NewLookDefault = true;

        /// <param name="playerChoice">-1 no choice yet, 0 off, 1 on.</param>
        public static bool NewLook(int playerChoice) => playerChoice < 0 ? NewLookDefault : playerChoice == 1;

        /// <summary>Block patterns are opt in: a shape on each colour so colour is never the only cue (docs/FEEL.md).</summary>
        public static bool Patterns(int playerChoice) => playerChoice == 1;

        /// <summary>One distinct shape per block colour (0-4). Level obstacles (stone) and anything else get none.</summary>
        public static BlockPattern PatternFor(int colorIndex) => colorIndex switch
        {
            0 => BlockPattern.Dot,
            1 => BlockPattern.Stripes,
            2 => BlockPattern.Ring,
            3 => BlockPattern.Cross,
            4 => BlockPattern.Diamond,
            _ => BlockPattern.None,
        };
    }

    public enum BlockPattern { None, Dot, Stripes, Ring, Cross, Diamond }

    public readonly struct NearMiss
    {
        public readonly int X, Y, Lines;
        public bool Found => Lines > 0;
        public NearMiss(int x, int y, int lines) { X = x; Y = y; Lines = lines; }

        /// <summary>At game over: the empty cell that, filled, would have completed the most rows and columns.
        /// Used for the honest "one cell away" message: it only reports a miss that really happened.</summary>
        public static NearMiss Find(Board b)
        {
            int n = b.Size;
            var rowEmpty = new int[n];
            var colEmpty = new int[n];
            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                    if (!b.IsFilled(x, y)) { rowEmpty[y]++; colEmpty[x]++; }
            var best = new NearMiss(-1, -1, 0);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    if (b.IsFilled(x, y)) continue;
                    int lines = (rowEmpty[y] == 1 ? 1 : 0) + (colEmpty[x] == 1 ? 1 : 0);
                    if (lines > best.Lines) best = new NearMiss(x, y, lines);
                }
            return best;
        }

        /// <summary>The message to show, as a <see cref="Loc"/> key and its argument, or null when there was no near miss.</summary>
        public string Message() => Lines >= 2 ? Loc.F("One cell away from a {0}-line clear!", Lines) : Lines == 1 ? Loc.L("One cell away from a clear!") : null;
    }
}
