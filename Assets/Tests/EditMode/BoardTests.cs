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
