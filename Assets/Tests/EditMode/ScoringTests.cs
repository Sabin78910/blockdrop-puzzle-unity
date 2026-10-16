using System.Linq;
using BlockDrop.Core;
using NUnit.Framework;

public class ScoringTests
{
    private static Piece Line(int n) => new Piece(Enumerable.Range(0, n).Select(i => (i, 0)).ToArray(), 0);
    private static readonly Piece Dot = new Piece(new[] { (0, 0) }, 1);

    /// <summary>Fills row <paramref name="y"/> except its last cell, then completes it.</summary>
    private static PlaceResult ClearRow(Board b, int y)
    {
        b.Place(Line(7), 0, y);
        return b.Place(Dot, 7, y);
    }

    [Test] public void ComboMultipliesConsecutiveClears()
    {
        var b = new Board(8);
        for (int y = 0; y < 3; y++) b.Place(Line(7), 0, y); // three rows one cell short
        Assert.AreEqual(1 + 10 * 1, b.Place(Dot, 7, 0).Points); // combo 0 → x1
        Assert.AreEqual(1 + 10 * 2, b.Place(Dot, 7, 1).Points); // combo 1 → x2
        Assert.AreEqual(1 + 10 * 3, b.Place(Dot, 7, 2).Points); // combo 2 → x3
        Assert.AreEqual(3, b.Combo);
    }

    [Test] public void PlacementWithoutClearResetsCombo()
    {
        var b = new Board(8);
        ClearRow(b, 0);
        b.Place(Line(7), 0, 1); // no clear
        Assert.AreEqual(0, b.Combo);
        Assert.AreEqual(1 + 10, b.Place(Dot, 7, 1).Points, "multiplier starts again at x1");
    }

    [Test] public void MultiLineClearScoresLinesSquared()
    {
        var b = new Board(8);
        b.Place(Line(7), 0, 0);
        b.Place(Line(7), 0, 1);
        var bar = new Piece(new[] { (0, 0), (0, 1) }, 2);
        var r = b.Place(bar, 7, 0);
        Assert.AreEqual(2, r.LinesCleared);
        Assert.AreEqual(2 + 10 * 2 * 2, r.Points);
    }

    [Test] public void ScoreAccumulatesEveryPlacement()
    {
        var b = new Board(8);
        int total = b.Place(Line(3), 0, 5).Points;
        total += b.Place(Line(7), 0, 0).Points;
        total += b.Place(Dot, 7, 0).Points;
        Assert.AreEqual(3 + 7 + (1 + 10), total);
        Assert.AreEqual(total, b.Score);
    }
}
