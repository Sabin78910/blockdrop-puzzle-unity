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

        /// <summary>Each chapter has its own look; the sky colour blends from one to the next as you climb.</summary>
        public static readonly string[] ThemeNames = { "Sunny Meadow", "Pink Dusk", "Blue Lagoon", "Violet Night", "Golden Dunes", "Mint Frost" };
        public static readonly string[] ThemeColors = { "7FD66B", "FF9EC4", "49C6E5", "5B3FB8", "F2C46B", "A8E6D9" };

        /// <summary>File names of the generated backdrops in Resources/Map (tools/art/make_map_art.py).</summary>
        public static readonly string[] ThemeKeys = { "meadow", "dusk", "lagoon", "night", "dunes", "frost" };

        public static int ThemeOf(int chapter) => (chapter - 1) % ThemeNames.Length;

        /// <summary>0..1 position between chapter themes for a (fractional) level, used to blend the backdrop.</summary>
        public static (int from, int to, float t) Blend(float level)
        {
            float f = Math.Max(0f, (level - 1f) / ChapterSize);
            int a = (int)Math.Floor(f);
            float t = f - a;
            return (ThemeOf(a + 1), ThemeOf(a + 2), t * t * (3f - 2f * t));
        }

        /// <summary>Scenery beside a level's node: kind 0 tree, 1 house, 2 lamp, -1 none; x is 0..1 on the side
        /// away from the node. Same for every player.</summary>
        public static (int kind, float x) PropFor(int level)
        {
            unchecked
            {
                uint z = (uint)level * 0x9E3779B9u; z = (z ^ (z >> 15)) * 0x85EBCA6Bu; z ^= z >> 13;
                int kind = z % 3 == 0 ? -1 : (int)((z >> 4) % 3);
                float side = NodeX(level) < 0.5f ? 1f : -1f;
                return (kind, 0.5f + side * (0.3f + (z >> 8) % 10 * 0.01f));
            }
        }

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
