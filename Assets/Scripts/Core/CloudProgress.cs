using System;

namespace BlockDrop.Core
{
    /// <summary>The player's saved progress as one short string for cloud storage, and the rule for combining
    /// this phone's copy with the cloud copy. Merging never loses progress: level stars and best score keep the
    /// larger value, and <see cref="PlayerMeta.Merge"/> handles coins, themes and achievements.
    /// Format: <c>1|best|progress|meta</c> (neither progress nor meta contains '|').</summary>
    public static class CloudProgress
    {
        public const string Version = "1";

        public static string Pack(string progress, string meta, int best) =>
            Version + "|" + Math.Max(0, best) + "|" + (progress ?? "") + "|" + (meta ?? "");

        public static bool TryUnpack(string blob, out string progress, out string meta, out int best)
        {
            progress = ""; meta = ""; best = 0;
            if (string.IsNullOrEmpty(blob)) return false;
            var parts = blob.Split(new[] { '|' }, 4);
            if (parts.Length != 4 || parts[0] != Version || !int.TryParse(parts[1], out best)) return false;
            best = Math.Max(0, best);
            progress = parts[2]; meta = parts[3];
            return true;
        }

        /// <summary>Combines two packed copies. If one is unreadable the other is returned unchanged.</summary>
        public static string Merge(string a, string b)
        {
            bool okA = TryUnpack(a, out var pa, out var ma, out int ba);
            bool okB = TryUnpack(b, out var pb, out var mb, out int bb);
            if (!okA) return okB ? b : a;
            if (!okB) return a;

            var x = Progress.Parse(pa);
            var y = Progress.Parse(pb);
            var levels = new Progress();
            for (int i = 1; i <= LevelLibrary.Count; i++) levels.Record(i, Math.Max(x.StarsFor(i), y.StarsFor(i)));

            var meta = PlayerMeta.Merge(PlayerMeta.Parse(ma), PlayerMeta.Parse(mb));
            return Pack(levels.Serialize(), meta.Serialize(), Math.Max(ba, bb));
        }
    }
}
