using System.Collections.Generic;

namespace BlockDrop.Core
{
    public sealed class Picture
    {
        public string Name;
        public string[] Rows;                 // Collection.Width x Collection.Height pixel art
        public Dictionary<char, string> Colors;
    }

    /// <summary>Journey meta: every level beaten reveals one of 10 pieces of its chapter's picture (10 pictures for
    /// 100 levels). Like Block Blast's Adventure collection, but the levels stay fair (tuned by our playtester).</summary>
    public static class Collection
    {
        public const int Width = 15, Height = 10, TileCols = 5, TileW = 3, TileH = 5, TilesPerPicture = 10, CompletionReward = 250;

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
            new Picture { Name = "Gem", Rows = new[] {
                "kkkkkkkkkkkkkkk", "kkkkwcccccbkkkk", "kkkwwcccccbbkkk", "kkwwwwcccbbbbkk", "kkkwwccccbbbkkk",
                "kkkkwcccccbkkkk", "kkkkkwcccbkkkkk", "kkkkkkwcbkkkkkk", "kkkkkkkckkkkkkk", "kkkkkkkkkkkkkkk" },
                Colors = new Dictionary<char, string> { ['k'] = "1B1240", ['c'] = "2EC4F1", ['w'] = "BFF4FF", ['b'] = "0077B6" } },
            new Picture { Name = "Peaks", Rows = new[] {
                "sssssssssssssss", "ssssswsssssssss", "sssswwwssssswss", "ssswwrwwssswwws", "sswrrrrrwswwrws",
                "swrrdrrrrwrrrrw", "wrrddrrrrrrdrrr", "rrdddrrrrrdddrr", "ggggggggggggggg", "ggggggggggggggg" },
                Colors = new Dictionary<char, string> { ['s'] = "8FD3FF", ['w'] = "FFFFFF", ['r'] = "6B7390", ['d'] = "3E4560", ['g'] = "4BAF6A" } },
            new Picture { Name = "Moon", Rows = new[] {
                "nnwnnnnnnnnnwnn", "nnnnnnyyynnnnnn", "nnnnnyyyynnnwnn", "nwnnyyynnnnnnnn", "nnnnyyynnnnnnnn",
                "nnnnyyyynnnnnwn", "nnnnnyyyyynnnnn", "nnwnnnyyynnnnnn", "nnnnccccnnnnccc", "ccccccccccccccc" },
                Colors = new Dictionary<char, string> { ['n'] = "15154A", ['y'] = "FFE27A", ['w'] = "FFFFFF", ['c'] = "6D5DFC" } },
            new Picture { Name = "Reef", Rows = new[] {
                "bbbbbbbbbbbubbb", "bbbbbbbbbbbbbub", "bbbboooobbbbubb", "bbookoooooboobb", "boooooooooooobb",
                "bbooooooooboobb", "bbbboooobbbbobb", "bgbbbbbbbbbbbgb", "ggbbgbbbbbgbbgg", "sssssssssssssss" },
                Colors = new Dictionary<char, string> { ['b'] = "0077B6", ['o'] = "FF8C42", ['k'] = "15151F", ['g'] = "2EC4B6", ['s'] = "F7C59F", ['u'] = "90E0EF" } },
            new Picture { Name = "Blossom", Rows = new[] {
                "aaaaaaaaaaaaaaa", "aaaaaapppaaaaaa", "aaaappyyyppaaaa", "aaapppyyypppaaa", "aaaappyyyppaaaa",
                "aaaaaapppaaaaaa", "aaaaaaagaaaaaaa", "aaaggaagaaggaaa", "llllllgggllllll", "lllllllllllllll" },
                Colors = new Dictionary<char, string> { ['a'] = "A0E7FF", ['p'] = "FF8FAB", ['y'] = "FFD166", ['g'] = "4BAF6A", ['l'] = "9BF6C4" } },
            new Picture { Name = "Castle", Rows = new[] {
                "nwnrnnnnnnnrnwn", "nnnsnnnnnnnsnnn", "nnsssnnnnnsssnn", "nnsssnsnsnsssnn", "nnsssssssssssnn",
                "nnsdsssssssdsnn", "nnsssskkkssssnn", "nnsssskkkssssnn", "ggggggggggggggg", "ggggggggggggggg" },
                Colors = new Dictionary<char, string> { ['n'] = "2A0E61", ['w'] = "FFFFFF", ['r'] = "FF4D6D", ['s'] = "C9C9D9", ['d'] = "8A8AA3", ['k'] = "4A2C12", ['g'] = "2EE59D" } },
            new Picture { Name = "Crown", Rows = new[] {
                "ppppppppppppppp", "ppyppppyppppypp", "ppyyppyyyppyypp", "ppyyyyyyyyyyypp", "ppyyryybyyryypp",
                "ppyyyyyyyyyyypp", "ppooooooooooopp", "ppppppppppppppp", "pppwpppppppwppp", "ppppppppppppppp" },
                Colors = new Dictionary<char, string> { ['p'] = "6D2E8C", ['y'] = "FFD54A", ['o'] = "D99A00", ['r'] = "FF4D6D", ['b'] = "2EC4F1", ['w'] = "FFFFFF" } },
        };

        /// <summary>Only the launch levels have picture pieces; generated levels beyond them do not.</summary>
        public static bool Covers(int level) => level >= 1 && level <= Pictures.Length * TilesPerPicture;

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
