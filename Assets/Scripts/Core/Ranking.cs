using System.Globalization;

namespace BlockDrop.Core
{
    /// <summary>Presentation rules for leaderboard rows: no photos or extra data, only what the player ID already gives us.</summary>
    public static class Ranking
    {
        public const int EmblemCount = 6;

        /// <summary>Splits "Name#1234" into ("Name", "#1234"); the tag is empty when there is none.</summary>
        public static void SplitName(string playerName, out string name, out string tag)
        {
            playerName = string.IsNullOrEmpty(playerName) ? "Player" : playerName;
            int hash = playerName.IndexOf('#');
            name = hash > 0 ? playerName.Substring(0, hash) : playerName;
            tag = hash > 0 ? playerName.Substring(hash) : "";
        }

        /// <summary>Stable 0..EmblemCount-1 from the player ID (FNV-1a, so it never changes between runs or devices).</summary>
        public static int Emblem(string playerName)
        {
            uint h = 2166136261;
            foreach (char c in playerName ?? "") { h ^= c; h *= 16777619; }
            h ^= h >> 15; h *= 2246822519; h ^= h >> 13;
            return (int)(h % EmblemCount);
        }

        public static string Initial(string playerName)
        {
            SplitName(playerName, out var n, out _);
            foreach (char c in n) if (char.IsLetterOrDigit(c)) return char.ToUpperInvariant(c).ToString();
            return "?";
        }

        /// <summary>1 gold, 2 silver, 3 bronze, 0 none.</summary>
        public static int Medal(int rank) => rank >= 1 && rank <= 3 ? rank : 0;

        public static string FormatScore(long score) => score.ToString("N0", CultureInfo.InvariantCulture);
    }
}
