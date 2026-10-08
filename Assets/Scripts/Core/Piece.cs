using System;
using System.Collections.Generic;

namespace BlockDrop.Core
{
    /// <summary>A polyomino described by cell offsets from its top-left corner.</summary>
    public sealed class Piece
    {
        public readonly (int x, int y)[] Cells;
        public readonly int ColorIndex;

        public Piece((int x, int y)[] cells, int colorIndex)
        {
            if (cells == null || cells.Length == 0) throw new ArgumentException("Piece needs cells");
            Cells = cells;
            ColorIndex = colorIndex;
        }

        public int Width { get { int w = 0; foreach (var c in Cells) w = Math.Max(w, c.x + 1); return w; } }
        public int Height { get { int h = 0; foreach (var c in Cells) h = Math.Max(h, c.y + 1); return h; } }

        public static readonly IReadOnlyList<(int x, int y)[]> Shapes = new[]
        {
            new[] { (0, 0) },
            new[] { (0, 0), (1, 0) },
            new[] { (0, 0), (0, 1) },
            new[] { (0, 0), (1, 0), (2, 0) },
            new[] { (0, 0), (0, 1), (0, 2) },
            new[] { (0, 0), (1, 0), (0, 1), (1, 1) },
            new[] { (0, 0), (1, 0), (2, 0), (3, 0) },
            new[] { (0, 0), (0, 1), (0, 2), (0, 3) },
            new[] { (0, 0), (0, 1), (1, 1) },
            new[] { (1, 0), (0, 1), (1, 1) },
            new[] { (0, 0), (1, 0), (2, 0), (1, 1) },
            new[] { (0, 0), (0, 1), (0, 2), (1, 2), (2, 2) },
            new[] { (0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1), (0, 2), (1, 2), (2, 2) },
        };
    }

    /// <summary>Deterministic piece source: same seed gives the same sequence (daily challenge).</summary>
    public sealed class PieceGenerator
    {
        private readonly Random _rng;
        public PieceGenerator(int seed) { _rng = new Random(seed); }

        public Piece Next()
        {
            int i = _rng.Next(Piece.Shapes.Count);
            return new Piece(Piece.Shapes[i], _rng.Next(5));
        }

        public static int DailySeed(DateTime dateUtc) => dateUtc.Year * 10000 + dateUtc.Month * 100 + dateUtc.Day;
    }
}
