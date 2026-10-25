using System;
using System.Globalization;
using System.Text;

namespace BlockDrop.Core
{
    /// <summary>A run in progress, saved so a phone killing the app mid-game doesn't lose it.
    /// Stores the board, hand and the piece generator's position, so the exact same pieces keep coming
    /// (daily and challenge runs stay fair). Serialized to one short line for PlayerPrefs.</summary>
    public sealed class SavedGame
    {
        private const string Version = "BD1";
        private const int FieldCount = 19, OldFieldCount = 17; // saves from before level continues have 17 fields

        public string Mode = "";
        public string Day = "";
        public int Seed, Level, Hints;
        public bool FairHands;
        public int Size = 8;
        public int[] Cells = Array.Empty<int>(); // x + y * Size, Board.Empty for empty
        public int Score, Combo, Placements, Lines, Draws, Moves, Revives; // Placements and Lines feed the server score check
        public int Extra, Continues; // level moves added by the helping hand or by continuing
        public int[] HandShapes = new int[Dealer.HandSize]; // -1 = slot used
        public int[] HandColors = new int[Dealer.HandSize];

        public static SavedGame Capture(string mode, Seat seat, bool fairHands, int level, string day, int hints, int extra = 0, int continues = 0)
        {
            var b = seat.Board;
            var g = new SavedGame
            {
                Mode = mode, Day = day ?? "", Seed = seat.Dealer.Seed, Level = level, Hints = hints, FairHands = fairHands,
                Size = b.Size, Cells = new int[b.Size * b.Size], Score = b.Score, Combo = b.Combo, Placements = b.Placements, Lines = b.LinesCleared,
                Draws = seat.Dealer.Draws, Moves = seat.Moves, Revives = seat.Revives, Extra = extra, Continues = continues,
            };
            for (int x = 0; x < b.Size; x++)
                for (int y = 0; y < b.Size; y++)
                    g.Cells[x + y * b.Size] = b[x, y];
            for (int i = 0; i < Dealer.HandSize; i++)
            {
                var p = seat.Dealer.Hand[i];
                g.HandShapes[i] = p?.ShapeIndex ?? -1;
                g.HandColors[i] = p?.ColorIndex ?? 0;
            }
            return g;
        }

        /// <summary>A fresh seat (same seed and dealing rule) with this save applied.</summary>
        public Seat NewSeat()
        {
            var seat = new Seat(Seed, Size, FairHands);
            ApplyTo(seat);
            return seat;
        }

        /// <summary>Overwrites <paramref name="seat"/> (created with the same seed and dealing rule) with this save.</summary>
        public void ApplyTo(Seat seat)
        {
            var b = seat.Board;
            if (b.Size != Size) throw new ArgumentException("Board size differs from save");
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    b.SetCell(x, y, Cells[x + y * Size]);
            b.RestoreScore(Score, Combo, Placements, Lines);
            var hand = new Piece[Dealer.HandSize];
            for (int i = 0; i < hand.Length; i++)
                hand[i] = HandShapes[i] < 0 ? null : new Piece(Piece.Shapes[HandShapes[i]], HandColors[i]);
            seat.Dealer.Restore(Draws, hand);
            seat.RestoreCounters(Moves, Revives);
        }

        public string Serialize()
        {
            var cells = new StringBuilder(Cells.Length);
            foreach (int c in Cells) cells.Append(c == Board.Empty ? '.' : (char)('0' + c));
            var hand = new string[Dealer.HandSize];
            for (int i = 0; i < hand.Length; i++) hand[i] = HandShapes[i] < 0 ? "-" : HandShapes[i] + ":" + HandColors[i];
            return string.Join("|", Version, Mode, I(Seed), I(Level), Day, FairHands ? "1" : "0", I(Size), cells.ToString(),
                I(Score), I(Combo), string.Join(",", hand), I(Draws), I(Moves), I(Revives), I(Hints), I(Placements), I(Lines), I(Extra), I(Continues));
        }

        /// <summary>Null for anything missing, old, corrupt or impossible: a bad save must never stop the game launching.</summary>
        public static SavedGame Parse(string data)
        {
            if (string.IsNullOrEmpty(data)) return null;
            var f = data.Split('|');
            if ((f.Length != FieldCount && f.Length != OldFieldCount) || f[0] != Version) return null;
            var g = new SavedGame { Mode = f[1], Day = f[4], FairHands = f[5] == "1" };
            if (!N(f[2], out g.Seed) || !N(f[3], out g.Level) || !N(f[6], out g.Size) || !N(f[8], out g.Score) || !N(f[9], out g.Combo)
                || !N(f[11], out g.Draws) || !N(f[12], out g.Moves) || !N(f[13], out g.Revives) || !N(f[14], out g.Hints)
                || !N(f[15], out g.Placements) || !N(f[16], out g.Lines)) return null;
            if (f.Length == FieldCount && (!N(f[17], out g.Extra) || !N(f[18], out g.Continues) || g.Extra < 0 || g.Continues < 0)) return null;
            if (g.Mode == "" || g.Size < 4 || g.Size > 16 || f[7].Length != g.Size * g.Size) return null;
            if (g.Score < 0 || g.Combo < 0 || g.Draws < 0 || g.Draws > 1000000 || g.Moves < 0 || g.Revives < 0 || g.Hints < 0 || g.Placements < 0 || g.Lines < 0) return null;
            g.Cells = new int[g.Size * g.Size];
            for (int i = 0; i < g.Cells.Length; i++)
            {
                char c = f[7][i];
                if (c == '.') g.Cells[i] = Board.Empty;
                else if (c >= '0' && c <= '0' + Board.MaxCell) g.Cells[i] = c - '0';
                else return null;
            }
            var hand = f[10].Split(',');
            if (hand.Length != Dealer.HandSize) return null;
            bool any = false;
            for (int i = 0; i < hand.Length; i++)
            {
                if (hand[i] == "-") { g.HandShapes[i] = -1; continue; }
                var sc = hand[i].Split(':');
                if (sc.Length != 2 || !N(sc[0], out g.HandShapes[i]) || !N(sc[1], out g.HandColors[i])) return null;
                if (g.HandShapes[i] < 0 || g.HandShapes[i] >= Piece.Shapes.Count || g.HandColors[i] < 0 || g.HandColors[i] >= Board.Stone) return null;
                any = true;
            }
            return any ? g : null; // the dealer always refills an empty hand, so a save without pieces is broken
        }

        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        private static bool N(string s, out int v) =>
            int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v);
    }
}
