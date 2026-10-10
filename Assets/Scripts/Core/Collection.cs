using System.Collections.Generic;

namespace BlockDrop.Core
{
    public sealed class Picture
    {
        public string Name;
        public string[] Rows;                 // Collection.Width x Collection.Height pixel art
        public Dictionary<char, string> Colors;
    }

    /// <summary>Journey meta: every level beaten reveals one of 10 pieces of a picture (3 pictures for 30 levels).
    /// Like Block Blast's Adventure collection, but the levels stay fair (calibrated by our bot).</summary>
    public static class Collection
    {
        public const int Width = 15, Height = 10, TileCols = 5, TileW = 3, TileH = 5, TilesPerPicture = 10, CompletionReward = 100;

        public static readonly Picture[] Pictures =
        {
            new Picture { Name = "Sunset", Rows = new[] {
                "aaaaaaaaaaaaaaa", "aaaaaaasssaaaaa", "bbbbbbsssssbbbb", "bbbbbbsssssbbbb", "cccccccsssccccc",
                "ccmcccccccccmcc", "cmmmcccmccccmmm", "mmmmmcmmmcmmmmm", "mmmmmmmmmmmmmmm", "ggggggggggggggg" },
                Colors = new Dictionary<char, string> { ['a'] = "6D5DFC", ['b'] = "FF6FB5", ['c'] = "FFB627", ['s'] = "FFE27A", ['m'] = "2A0E61", ['g'] = "2EE59D" } },
            new Picture { Name = "Rocket", Rows = new[] {
                "nwnnnnnrnnnnnwn", "nnnnnnrlrnnnnnn", "nnnwnnlllnnnwnn", "nnnnnnlblnnnnnn", "nnnnnnlllnnnnnn",
                "nwnnnrlllrnnnnn", "nnnnrrlllrrnnwn", "nnnnnnfffnnnnnn", "nnwnnnnfnnnnnnn", "nnnnnnnnnnnwnnn" },
                Colors = new Dictionary<char, string> { ['n'] = "15151F", ['w'] = "FFFFFF", ['r'] = "FF4D6D", ['l'] = "E6E6F0", ['b'] = "2EC4F1", ['f'] = "FFB627" } },
            new Picture { Name = "Heart", Rows = new[] {
                "ppppppppppppppp", "pppprrppprrpppp", "ppprwrrprrrrppp", "pprrrrrrrrrrrpp", "pprrrrrrrrrrdpp",
                "ppprrrrrrrrdppp", "pppprrrrrrdpppp", "pppppprrrdppppp", "pppppppdppppppp", "ppppppppppppppp" },
                Colors = new Dictionary<char, string> { ['p'] = "FFD6E8", ['r'] = "FF4D6D", ['d'] = "C9184A", ['w'] = "FFFFFF" } },
        };

        public static (int picture, int tile) ForLevel(int level) => ((level - 1) / TilesPerPicture, (level - 1) % TilesPerPicture);

        public static int TileAt(int x, int y) => (y / TileH) * TileCols + x / TileW;

        public static bool Revealed(Progress p, int picture, int tile) => p.StarsFor(picture * TilesPerPicture + tile + 1) > 0;

        public static int RevealedCount(Progress p, int picture)
        {
            int n = 0;
            for (int t = 0; t < TilesPerPicture; t++) if (Revealed(p, picture, t)) n++;
            return n;
        }

        public static bool Complete(Progress p, int picture) => RevealedCount(p, picture) == TilesPerPicture;

        public static int CompletedPictures(Progress p)
        {
            int n = 0;
            for (int i = 0; i < Pictures.Length; i++) if (Complete(p, i)) n++;
            return n;
        }
    }
}
