using System.Collections.Generic;

namespace BlockDrop.Core
{
    public readonly struct PlaceResult
    {
        public readonly int LinesCleared;
        public readonly int Points;
        public PlaceResult(int lines, int points) { LinesCleared = lines; Points = points; }
    }

    /// <summary>Pure game rules for an N×N block puzzle. No Unity dependencies, fully unit-testable.</summary>
    public sealed class Board
    {
        public const int Empty = -1;
        /// <summary>Level blockers. Blocks a player places use colours 0-4; these never come from a hand.
        /// Stone clears with its line; a gem is a stone worth collecting; ice takes two clears (first it cracks to stone).</summary>
        public const int Stone = 5, Gem = 6, Ice = 7, MaxCell = Ice;
        public readonly int Size;
        private readonly int[,] _cells;
        public int Score { get; private set; }
        public int Combo { get; private set; }
        /// <summary>Pieces placed and lines cleared so far; sent with scores so the server can sanity-check them.</summary>
        public int Placements { get; private set; }
        public int LinesCleared { get; private set; }

        public Board(int size = 8)
        {
            Size = size;
            _cells = new int[size, size];
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    _cells[x, y] = Empty;
        }

        public int this[int x, int y] => _cells[x, y];
        public bool IsFilled(int x, int y) => _cells[x, y] != Empty;

        /// <summary>Sets a cell directly (level obstacles). Does not score or clear lines.</summary>
        public void SetCell(int x, int y, int color) => _cells[x, y] = color;

        /// <summary>Rows and columns that placing <paramref name="piece"/> at (ox, oy) would clear,
        /// without changing the board (used for the glow preview and clear animations).</summary>
        public (List<int> rows, List<int> cols) PreviewClears(Piece piece, int ox, int oy)
        {
            var rows = new List<int>();
            var cols = new List<int>();
            if (!CanPlace(piece, ox, oy)) return (rows, cols);
            var filled = new bool[Size, Size];
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    filled[x, y] = IsFilled(x, y);
            foreach (var (cx, cy) in piece.Cells) filled[ox + cx, oy + cy] = true;
            for (int i = 0; i < Size; i++)
            {
                bool row = true, col = true;
                for (int j = 0; j < Size; j++)
                {
                    if (!filled[j, i]) row = false;
                    if (!filled[i, j]) col = false;
                }
                if (row) rows.Add(i);
                if (col) cols.Add(i);
            }
            return (rows, cols);
        }

        /// <summary>Puts back a saved score and counters (resuming a game). Cells are restored with <see cref="SetCell"/>.</summary>
        public void RestoreScore(int score, int combo, int placements, int linesCleared)
        {
            Score = score; Combo = combo; Placements = placements; LinesCleared = linesCleared;
        }

        public Board Clone()
        {
            var b = new Board(Size) { Score = Score, Combo = Combo, Placements = Placements, LinesCleared = LinesCleared };
            System.Array.Copy(_cells, b._cells, _cells.Length);
            return b;
        }

        public bool CanPlace(Piece piece, int ox, int oy)
        {
            foreach (var (cx, cy) in piece.Cells)
            {
                int x = ox + cx, y = oy + cy;
                if (x < 0 || y < 0 || x >= Size || y >= Size || IsFilled(x, y)) return false;
            }
            return true;
        }

        public bool CanPlaceAnywhere(Piece piece)
        {
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    if (CanPlace(piece, x, y)) return true;
            return false;
        }

        /// <summary>Places a piece, clears full rows/columns, and scores.
        /// Points: 1 per cell placed + 10 per line × line count × (1 + combo).</summary>
        public PlaceResult Place(Piece piece, int ox, int oy)
        {
            if (!CanPlace(piece, ox, oy)) throw new System.InvalidOperationException("Invalid placement");
            foreach (var (cx, cy) in piece.Cells) _cells[ox + cx, oy + cy] = piece.ColorIndex;

            var rows = new List<int>();
            var cols = new List<int>();
            for (int i = 0; i < Size; i++)
            {
                bool row = true, col = true;
                for (int j = 0; j < Size; j++)
                {
                    if (!IsFilled(j, i)) row = false;
                    if (!IsFilled(i, j)) col = false;
                }
                if (row) rows.Add(i);
                if (col) cols.Add(i);
            }
            // mark first so a cell where a row and column cross is only hit once (ice cracks once per move)
            var hit = new bool[Size, Size];
            foreach (int y in rows) for (int x = 0; x < Size; x++) hit[x, y] = true;
            foreach (int x in cols) for (int y = 0; y < Size; y++) hit[x, y] = true;
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    if (hit[x, y]) _cells[x, y] = _cells[x, y] == Ice ? Stone : Empty;

            int lines = rows.Count + cols.Count;
            int points = piece.Cells.Length;
            if (lines > 0)
            {
                points += 10 * lines * lines * (1 + Combo);
                Combo++;
            }
            else
            {
                Combo = 0;
            }
            Score += points;
            Placements++;
            LinesCleared += lines;
            return new PlaceResult(lines, points);
        }

        /// <summary>The hammer booster: breaks one block without scoring. Ice takes the first hit and cracks to stone,
        /// as it would from a line clear. Returns false for an empty cell or one off the board.</summary>
        public bool Smash(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size || !IsFilled(x, y)) return false;
            _cells[x, y] = _cells[x, y] == Ice ? Stone : Empty;
            return true;
        }

        /// <summary>How many cells hold <paramref name="cell"/> (used for level goals: stones left, gems left).</summary>
        public int Count(int cell)
        {
            int n = 0;
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    if (_cells[x, y] == cell) n++;
            return n;
        }

        public bool IsGameOver(IEnumerable<Piece> hand)
        {
            foreach (var p in hand)
                if (p != null && CanPlaceAnywhere(p)) return false;
            return true;
        }
    }
}
