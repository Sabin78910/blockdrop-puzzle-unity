using System;
using System.Linq;
using BlockDrop.Core;
using NUnit.Framework;

public class BoardTests
{
    private static Piece Line(int n) => new Piece(Enumerable.Range(0, n).Select(i => (i, 0)).ToArray(), 0);
    private static readonly Piece Dot = new Piece(new[] { (0, 0) }, 1);

    [Test] public void RejectsOutOfBoundsAndOverlap()
    {
        var b = new Board(8);
        Assert.IsFalse(b.CanPlace(Line(3), 6, 0));
        b.Place(Dot, 0, 0);
        Assert.IsFalse(b.CanPlace(Dot, 0, 0));
        Assert.Throws<InvalidOperationException>(() => b.Place(Dot, 0, 0));
    }

    [Test] public void ClearsFullRowAndScores()
    {
        var b = new Board(8);
        b.Place(Line(4), 0, 3);
        var r = b.Place(Line(4), 4, 3);
        Assert.AreEqual(1, r.LinesCleared);
        Assert.AreEqual(4 + 10, r.Points);
        for (int x = 0; x < 8; x++) Assert.IsFalse(b.IsFilled(x, 3));
        Assert.AreEqual(1, b.Combo);
    }

    [Test] public void CountsPlacementsAndLinesForScoreChecks()
    {
        var b = new Board(8);
        b.Place(Line(4), 0, 3);
        b.Place(Line(4), 4, 3);
        Assert.AreEqual(2, b.Placements);
        Assert.AreEqual(1, b.LinesCleared);
        var copy = b.Clone();
        Assert.AreEqual(2, copy.Placements);
        Assert.AreEqual(1, copy.LinesCleared);
    }

    [Test] public void ClearsRowAndColumnTogether()
    {
        var b = new Board(3);
        b.Place(new Piece(new[] { (0, 0), (1, 0) }, 0), 0, 0);
        b.Place(new Piece(new[] { (0, 0), (0, 1) }, 0), 2, 1);
        var r = b.Place(Dot, 2, 0);
        Assert.AreEqual(2, r.LinesCleared);
    }

    [Test] public void SameSeedSameSequence()
    {
        var a = new PieceGenerator(PieceGenerator.DailySeed(new DateTime(2026, 10, 8)));
        var c = new PieceGenerator(20261008);
        for (int i = 0; i < 20; i++) Assert.AreEqual(a.Next().Cells, c.Next().Cells);
    }

    [Test] public void GameOverWhenNothingFits()
    {
        var b = new Board(2);
        b.Place(Dot, 0, 0);
        var big = new Piece(new[] { (0, 0), (1, 0), (0, 1), (1, 1) }, 0);
        Assert.IsTrue(b.IsGameOver(new[] { big, null }));
        Assert.IsFalse(b.IsGameOver(new[] { Dot }));
    }
}

public class BlockerTests
{
    private static readonly Piece Single = new Piece(new[] { (0, 0) }, 0);

    private static void FillRowExcept(Board b, int y, int gap)
    {
        for (int x = 0; x < b.Size; x++) if (x != gap && !b.IsFilled(x, y)) b.SetCell(x, y, 1);
    }

    [Test] public void StoneAndGemClearWithTheirLine()
    {
        var b = new Board(8);
        b.SetCell(2, 4, Board.Stone);
        b.SetCell(5, 4, Board.Gem);
        FillRowExcept(b, 4, 0);
        Assert.AreEqual(1, b.Place(Single, 0, 4).LinesCleared);
        Assert.AreEqual(0, b.Count(Board.Stone));
        Assert.AreEqual(0, b.Count(Board.Gem));
        Assert.IsFalse(b.IsFilled(2, 4));
    }

    [Test] public void IceCracksToStoneThenClears()
    {
        var b = new Board(8);
        b.SetCell(3, 2, Board.Ice);
        FillRowExcept(b, 2, 0);
        b.Place(Single, 0, 2);
        Assert.AreEqual(Board.Stone, b[3, 2], "first clear cracks the ice");
        Assert.IsFalse(b.IsFilled(0, 2));
        FillRowExcept(b, 2, 0);
        b.Place(Single, 0, 2);
        Assert.IsFalse(b.IsFilled(3, 2), "second clear removes it");
    }

    [Test] public void IceWhereRowAndColumnCrossCracksOnlyOnce()
    {
        var b = new Board(8);
        b.SetCell(4, 4, Board.Ice);
        for (int k = 0; k < 8; k++)
        {
            if (k != 3 && k != 4) b.SetCell(k, 4, 1); // row 4 open only at x = 3
            if (k != 3 && k != 4) b.SetCell(4, k, 1); // column 4 open only at y = 3
        }
        var corner = new Piece(new[] { (0, 1), (1, 0) }, 0); // fills (3,4) and (4,3) in one move
        Assert.AreEqual(2, b.Place(corner, 3, 3).LinesCleared);
        Assert.AreEqual(Board.Stone, b[4, 4], "one move, one crack");
    }
}
