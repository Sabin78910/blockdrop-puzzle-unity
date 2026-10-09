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
        public readonly int Size;
        private readonly int[,] _cells;
        public int Score { get; private set; }
        public int Combo { get; private set; }

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

        public Board Clone()
        {
            var b = new Board(Size) { Score = Score, Combo = Combo };
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
            foreach (int y in rows) for (int x = 0; x < Size; x++) _cells[x, y] = Empty;
            foreach (int x in cols) for (int y = 0; y < Size; y++) _cells[x, y] = Empty;

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
            return new PlaceResult(lines, points);
        }

        public bool IsGameOver(IEnumerable<Piece> hand)
        {
            foreach (var p in hand)
                if (p != null && CanPlaceAnywhere(p)) return false;
            return true;
        }
    }
}
