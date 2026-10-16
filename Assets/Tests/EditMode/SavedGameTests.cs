using System.Linq;
using BlockDrop.Core;
using NUnit.Framework;

public class SavedGameTests
{
    private static void PlayGreedy(Seat seat, int moves)
    {
        for (int m = 0; m < moves && !seat.Out && Bot.TryBestMove(seat.Board, seat.Dealer.Hand, out int i, out int x, out int y); m++)
            Assert.IsTrue(seat.TryPlace(i, x, y));
    }

    private static string Cells(Board b)
    {
        var s = new char[b.Size * b.Size];
        for (int x = 0; x < b.Size; x++) for (int y = 0; y < b.Size; y++) s[x + y * b.Size] = b.IsFilled(x, y) ? (char)('0' + b[x, y]) : '.';
        return new string(s);
    }

    private static string Hand(Seat s) => string.Join(",", s.Dealer.Hand.Select(p => p == null ? "-" : p.ShapeIndex + ":" + p.ColorIndex));

    [Test] public void RoundTripRestoresBoardHandAndCounters()
    {
        var seat = new Seat(1234, fairHands: true);
        PlayGreedy(seat, 7); // mid-hand: some slots used
        var text = SavedGame.Capture("Classic", seat, true, 0, "2026-10-17", 2).Serialize();
        var g = SavedGame.Parse(text);
        Assert.IsNotNull(g);
        Assert.AreEqual("Classic", g.Mode);
        Assert.AreEqual("2026-10-17", g.Day);
        Assert.AreEqual(2, g.Hints);
        var back = g.NewSeat();
        Assert.AreEqual(Cells(seat.Board), Cells(back.Board));
        Assert.AreEqual(seat.Board.Score, back.Board.Score);
        Assert.AreEqual(seat.Board.Combo, back.Board.Combo);
        Assert.AreEqual(seat.Board.Placements, back.Board.Placements, "the server score check needs the real counts");
        Assert.AreEqual(seat.Board.LinesCleared, back.Board.LinesCleared);
        Assert.AreEqual(Hand(seat), Hand(back));
        Assert.AreEqual(seat.Moves, back.Moves);
        Assert.AreEqual(seat.Revives, back.Revives);
        Assert.AreEqual(seat.Out, back.Out);
    }

    [Test] public void ResumedGamePlaysOnExactlyLikeTheOriginal()
    {
        // Daily and challenge runs must deal the same pieces after a restore as without one.
        var original = new Seat(20261017);
        PlayGreedy(original, 5);
        var resumed = SavedGame.Parse(SavedGame.Capture("Daily", original, false, 0, "", 0).Serialize()).NewSeat();
        PlayGreedy(original, 40);
        PlayGreedy(resumed, 40);
        Assert.AreEqual(Cells(original.Board), Cells(resumed.Board));
        Assert.AreEqual(original.Board.Score, resumed.Board.Score);
        Assert.AreEqual(Hand(original), Hand(resumed));
        Assert.AreEqual(original.Board.Placements, resumed.Board.Placements);
        Assert.AreEqual(original.Board.LinesCleared, resumed.Board.LinesCleared);
    }

    [Test] public void LevelSaveKeepsObstaclesAndMovesLeft()
    {
        var level = new LevelSession(12);
        PlayGreedy(level.Seat, 4);
        var g = SavedGame.Parse(SavedGame.Capture("Level", level.Seat, false, 12, "", 3).Serialize());
        var resumed = new LevelSession(g.Level);
        g.ApplyTo(resumed.Seat);
        Assert.AreEqual(Cells(level.Seat.Board), Cells(resumed.Seat.Board));
        Assert.AreEqual(level.MovesLeft, resumed.MovesLeft);
        Assert.AreEqual(LevelState.Playing, resumed.State);
    }

    [Test] public void SaveAfterSecondChanceKeepsReviveCount()
    {
        var seat = new Seat(99, fairHands: true);
        PlayGreedy(seat, 500);
        Assert.IsTrue(seat.Out);
        seat.Revive();
        var back = SavedGame.Parse(SavedGame.Capture("Classic", seat, true, 0, "", 0).Serialize()).NewSeat();
        Assert.AreEqual(1, back.Revives, "a restored run must not offer a second second-chance");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("garbage")]
    [TestCase("BD0|Classic|1|0||1|8|................................................................|0|0|0:0,-,-|3|0|0|0|0|0")]
    [TestCase("BD1|Classic|1|0||1|8|.......|0|0|0:0,-,-|3|0|0|0|0|0")]
    [TestCase("BD1|Classic|1|0||1|8|...............................................................X|0|0|0:0,-,-|3|0|0|0|0|0")]
    [TestCase("BD1|Classic|1|0||1|8|................................................................|0|0|99:0,-,-|3|0|0|0|0|0")]
    [TestCase("BD1|Classic|1|0||1|8|................................................................|0|0|0:7,-,-|3|0|0|0|0|0")]
    [TestCase("BD1|Classic|1|0||1|8|................................................................|0|0|-,-,-|3|0|0|0|0|0")]
    [TestCase("BD1|Classic|1|0||1|8|................................................................|-5|0|0:0,-,-|3|0|0|0|0|0")]
    [TestCase("BD1|Classic|1|0||1|8|................................................................|0|0|0:0,-,-|3|0|0|0")] // older field count
    public void CorruptSavesAreIgnored(string data) => Assert.IsNull(SavedGame.Parse(data));

    [Test] public void ShapeIndexFindsEveryShape()
    {
        for (int i = 0; i < Piece.Shapes.Count; i++) Assert.AreEqual(i, new Piece(Piece.Shapes[i], 0).ShapeIndex);
        Assert.AreEqual(-1, new Piece(new[] { (0, 0), (2, 2) }, 0).ShapeIndex);
    }
}
