using System.Linq;
using System.Text;

namespace BlockDrop.Core
{
    /// <summary>Keeps public leaderboard names clean (audit finding V2). Checks English, Spanish, Portuguese
    /// and romanised Nepali, sees through leetspeak ("sh1t") and separators ("f_u_c_k"), and avoids
    /// false positives on ordinary words by matching short words only as whole tokens.</summary>
    public static class NameFilter
    {
        // Distinctive roots, blocked anywhere in the name.
        static readonly string[] Roots =
        {
            "fuck", "shit", "bitch", "cunt", "pussy", "whore", "slut", "nigg", "fagg", "retard", "rapist",
            "nazi", "hitler", "porn", "penis", "vagina", "bastard", "asshole", "dickhead", "motherf",
            "mierda", "pendej", "cabron", "caralho", "buceta", "porra", "merda", "viado", "putain",
            "machikne", "randi", "chikne", "bhalu", "mujiko", "muji",
        };

        // Short words that are only blocked as a whole token, so "Scunthorpe" or "Classic" pass.
        static readonly string[] Tokens = { "ass", "dick", "cock", "puta", "puto", "lado", "puti", "rape", "sex", "kkk" };

        static char Unleet(char c)
        {
            switch (c)
            {
                case '0': return 'o';
                case '1': return 'i';
                case '3': return 'e';
                case '4': return 'a';
                case '5': return 's';
                case '7': return 't';
                case '@': return 'a';
                case '$': return 's';
                default: return char.ToLowerInvariant(c);
            }
        }

        /// <summary>Letters only, leetspeak undone, separators removed: "Sh_1t" becomes "shit".</summary>
        public static string Squash(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "")
            {
                char u = Unleet(c);
                if (u >= 'a' && u <= 'z') sb.Append(u);
            }
            return sb.ToString();
        }

        /// <summary>True when the name is safe to show publicly.</summary>
        public static bool IsAllowed(string name)
        {
            string squashed = Squash(name);
            if (Roots.Any(squashed.Contains)) return false;
            var tokens = (name ?? "")
                .Split('_', '-', '.', ' ', '#')
                .Select(t => new string(t.Select(Unleet).Where(ch => ch >= 'a' && ch <= 'z').ToArray()))
                .Where(t => t.Length > 0);
            return !tokens.Any(t => Tokens.Contains(t));
        }

        /// <summary>For other players' names on the leaderboard: hides a bad name but keeps the #1234 tag.</summary>
        public static string ForDisplay(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return "Player";
            int hash = playerName.IndexOf('#');
            string baseName = hash >= 0 ? playerName.Substring(0, hash) : playerName;
            string tag = hash >= 0 ? playerName.Substring(hash) : "";
            return IsAllowed(baseName) ? playerName : "Player" + tag;
        }
    }
}
