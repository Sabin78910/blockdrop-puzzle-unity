using System.Linq;
using BlockDrop.Core;
using NUnit.Framework;

public class ModesTests
{
    [Test] public void DealerRefillsAfterThreePieces()
    {
        var d = new Dealer(42);
        var first = d.Hand.ToArray();
        d.Use(0); d.Use(1);
        Assert.IsNull(d.Hand[0]);
        Assert.AreSame(first[2], d.Hand[2]);
        d.Use(2);
        Assert.IsTrue(d.Hand.All(p => p != null));
    }

    [Test] public void VersusPlayersAlternateWithIdenticalPieces()
    {
        var m = new VersusMatch(123);
        CollectionAssert.AreEqual(m.Seats[0].Dealer.Hand.Select(p => p.Cells), m.Seats[1].Dealer.Hand.Select(p => p.Cells));
        Assert.AreEqual(0, m.Turn);
        Assert.IsTrue(Bot.TryBestMove(m.CurrentSeat.Board, m.CurrentSeat.Dealer.Hand, out int i, out int x, out int y));
        Assert.IsTrue(m.TryPlace(i, x, y));
        Assert.AreEqual(1, m.Turn);
    }

    [Test] public void VersusEndsWithAWinnerOrTie()
    {
        var m = new VersusMatch(99);
        // careless play (first legal spot) fills the boards quickly, so the match must end
        int guard = 0;
        while (!m.Over && guard++ < 5000)
        {
            bool moved = false;
            for (int i = 0; i < 3 && !moved; i++)
                for (int x = 0; x < 8 && !moved; x++)
                    for (int y = 0; y < 8 && !moved; y++)
                        moved = m.TryPlace(i, x, y);
            if (!moved) break;
        }
        Assert.IsTrue(m.Over, "match should end when both players are stuck");
        int s0 = m.Seats[0].Board.Score, s1 = m.Seats[1].Board.Score;
        Assert.AreEqual(s0 == s1 ? -1 : (s0 > s1 ? 0 : 1), m.Winner);
    }
}

public class AiAndPreviewTests
{
    [Test] public void PreviewClearsMatchesActualClear()
    {
        var b = new Board(8);
        var line4 = new Piece(new[] { (0, 0), (1, 0), (2, 0), (3, 0) }, 0);
        b.Place(line4, 0, 2);
        var (rows, cols) = b.PreviewClears(line4, 4, 2);
        CollectionAssert.AreEqual(new[] { 2 }, rows);
        Assert.IsEmpty(cols);
        Assert.IsTrue(b.IsFilled(0, 2), "preview must not change the board");
        Assert.AreEqual(1, b.Place(line4, 4, 2).LinesCleared);
    }

    [Test] public void EveryAiLevelMakesLegalMoves()
    {
        foreach (BotLevel level in System.Enum.GetValues(typeof(BotLevel)))
        {
            var seat = new Seat(5);
            var rng = new System.Random(1);
            for (int k = 0; k < 15 && !seat.Out; k++)
            {
                Assert.IsTrue(Bot.TryMove(seat.Board, seat.Dealer.Hand, level, rng, out int i, out int x, out int y), $"{level} found no move");
                Assert.IsTrue(seat.TryPlace(i, x, y), $"{level} chose an illegal move");
            }
        }
    }

    [Test] public void HarderAiScoresAtLeastAsWellOnAverage()
    {
        int Total(BotLevel level)
        {
            int sum = 0;
            for (int seed = 1; seed <= 6; seed++)
            {
                var seat = new Seat(seed * 31);
                var rng = new System.Random(seed);
                while (!seat.Out && seat.Moves < 40 && Bot.TryMove(seat.Board, seat.Dealer.Hand, level, rng, out int i, out int x, out int y))
                    seat.TryPlace(i, x, y);
                sum += seat.Board.Score;
            }
            return sum;
        }
        int easy = Total(BotLevel.Easy), hard = Total(BotLevel.Hard);
        Assert.Greater(hard, easy, $"hard {hard} vs easy {easy}");
    }
}
