using System;
using System.Collections.Generic;

namespace BlockDrop.Core
{
    /// <summary>One praise pop-up: the word (an English <see cref="Loc"/> key) and how big it is drawn.</summary>
    public readonly struct PraiseCue
    {
        public readonly string Word;
        public readonly int Rank;
        public PraiseCue(string word, int rank) { Word = word; Rank = rank; }
        public bool Any => Word != null;
        /// <summary>Text size multiplier: bigger praise for bigger moments.</summary>
        public float Size => 1.1f + 0.1f * Rank;
    }

    /// <summary>The praise ladder. The word follows whichever is bigger, the lines cleared in one move or the
    /// combo streak, so a long streak keeps earning new words. A lone line gets none (docs/FEEL.md: feedback
    /// matches the achievement). The words are the same in every mode; reduced motion only drops the movement.</summary>
    public static class Praise
    {
        private static readonly string[] Ladder = { null, "NICE!", "GREAT!", "EXCELLENT!", "AMAZING!", "UNBELIEVABLE!", "PERFECT CLEAR!" };

        public static int Rank(int lines, int combo, bool boardEmpty)
        {
            if (lines <= 0) return 0;
            if (boardEmpty) return 6;
            int byLines = lines >= 5 ? 5 : lines == 4 ? 4 : lines == 3 ? 3 : lines == 2 ? 2 : 0;
            int byCombo = combo >= 7 ? 5 : combo >= 5 ? 4 : combo == 4 ? 3 : combo == 3 ? 2 : combo == 2 ? 1 : 0;
            return Math.Max(byLines, byCombo);
        }

        public static PraiseCue For(int lines, int combo, bool boardEmpty)
        {
            int rank = Rank(lines, combo, boardEmpty);
            return new PraiseCue(Ladder[rank], rank);
        }
    }

    /// <summary>The first-run tutorial. Steps are stored as the same 0-3 number the game already keeps in
    /// PlayerPrefs ("tutorial"), on the phone only. It runs in Classic and can always be skipped.</summary>
    public static class Guide
    {
        public const int Done = 3;
        private const float ComboLinger = 3.5f;

        public static bool Active(int step, bool isClassic) => step < Done && isClassic;
        public static int AfterPlace(int step) => step == 0 ? 1 : step;
        public static int AfterClear(int step) => step == 1 ? 2 : step;
        public static int AfterWait(int step, float seconds) => step == 2 && seconds > ComboLinger ? Done : step;
        public static int Skip() => Done;

        /// <summary>What the guide says at each step (English keys for <see cref="Coach.T"/>).</summary>
        public static string Say(int step) => step switch
        {
            0 => "Drag a piece onto the board",
            1 => "Fill a row or column to clear it!",
            2 => "Clear again and again for COMBOS!",
            _ => null,
        };

        /// <summary>Whether the highlighted example move should show: only while the player still has to act.</summary>
        public static bool ShowExample(int step) => step == 0 || step == 1;
    }

    /// <summary>The numbers behind the level HUD: how far the next star is and how full the bar is.</summary>
    public static class Goal
    {
        public static int StarsEarned(int score, int one, int two, int three) => score >= three ? 3 : score >= two ? 2 : score >= one ? 1 : 0;

        /// <summary>Score still needed for the next star, or 0 when all three are earned.</summary>
        public static int ToNextStar(int score, int one, int two, int three)
        {
            int next = score < one ? one : score < two ? two : score < three ? three : 0;
            return next == 0 ? 0 : next - score;
        }

        public static float Fill(int score, int three) => three <= 0 ? 0f : Math.Min(1f, Math.Max(0f, (float)score / three));
        public static bool LowMoves(int movesLeft) => movesLeft <= 3;
    }

    /// <summary>Text for the HUD, praise and tutorial added in this change. Kept apart from <see cref="Loc"/>'s
    /// table; anything not listed here falls back to <see cref="Loc.L"/>.</summary>
    public static class Coach
    {
        public static readonly Dictionary<string, (string es, string pt)> Table = new Dictionary<string, (string, string)>
        {
            ["SKIP"] = ("SALTAR", "PULAR"),
            ["BLOCKY"] = ("BLOCKY", "BLOCKY"),
            ["HOME"] = ("INICIO", "INÍCIO"),
            ["STEP {0} OF {1}"] = ("PASO {0} DE {1}", "PASSO {0} DE {1}"),
            ["NEXT ★ IN {0}"] = ("PRÓX. ★ EN {0}", "PRÓX. ★ EM {0}"),
            ["ALL STARS!"] = ("¡TODAS LAS ★!", "TODAS AS ★!"),
            ["! MOVES {0}"] = ("! JUGADAS {0}", "! JOGADAS {0}"),
            ["MOVES {0}"] = ("JUGADAS {0}", "JOGADAS {0}"),
            ["SCORE"] = ("PUNTAJE", "PONTUAÇÃO"),
        };

        public static string T(string en)
        {
            if (Loc.Current != Lang.En && en != null && Table.TryGetValue(en, out var t)) return Loc.Current == Lang.Es ? t.es : t.pt;
            return Loc.L(en);
        }

        public static string F(string template, params object[] args) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, T(template), args);
    }
}
