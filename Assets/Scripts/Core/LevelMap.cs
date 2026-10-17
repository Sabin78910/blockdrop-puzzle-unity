using System;

namespace BlockDrop.Core
{
    /// <summary>Layout rules for the scrolling level map: a winding path that climbs one step per level, with a
    /// banner at the start of each chapter. Pure maths so it can be tested; the screen only draws it.</summary>
    public static class LevelMap
    {
        public const int ChapterSize = LevelLibrary.ChapterSize;

        /// <summary>Horizontal position of a level's node, 0..1 across the screen. Same for every player.</summary>
        public static float NodeX(int level) => 0.5f + 0.34f * (float)Math.Sin(level * 0.8);

        public static int ChapterOf(int level) => (level - 1) / ChapterSize + 1;

        public static bool StartsChapter(int level) => level >= 1 && (level - 1) % ChapterSize == 0;

        /// <summary>Levels worth drawing when <paramref name="bottomLevel"/> sits at the bottom edge and
        /// <paramref name="visibleSteps"/> map steps fit on screen (one spare step each side).</summary>
        public static (int first, int last) Visible(float bottomLevel, float visibleSteps)
        {
            int first = Math.Max(1, (int)Math.Floor(bottomLevel) - 1);
            int last = Math.Min(LevelLibrary.Count, (int)Math.Ceiling(bottomLevel + visibleSteps) + 1);
            return (first, last);
        }

        /// <summary>Where the map opens: the player's next level, a few steps up from the bottom so the
        /// path behind them is still in view.</summary>
        public static float StartBottom(int frontier) => Math.Max(0.5f, frontier - 3f);

        public static float ClampBottom(float bottomLevel, float visibleSteps) =>
            Math.Min(Math.Max(0.5f, bottomLevel), LevelLibrary.Count - visibleSteps + 1);
    }
}
