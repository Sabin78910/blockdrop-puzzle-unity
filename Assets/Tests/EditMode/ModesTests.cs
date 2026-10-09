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

    [Test] public void LevelsAreDeterministic()
    {
        var a = LevelLibrary.Get(7);
        var b = LevelLibrary.Get(7);
        Assert.AreEqual(a.Seed, b.Seed);
        CollectionAssert.AreEqual(a.Obstacles, b.Obstacles);
        Assert.AreEqual(a.TargetScore, b.TargetScore);
    }

    [Test] public void EveryLevelIsBeatableByTheBot()
    {
        for (int n = 1; n <= LevelLibrary.Count; n++)
        {
            var s = new LevelSession(n);
            while (s.State == LevelState.Playing && Bot.TryBestMove(s.Seat.Board, s.Seat.Dealer.Hand, out int i, out int x, out int y))
                Assert.IsTrue(s.TryPlace(i, x, y), $"level {n}: bot move rejected");
            Assert.AreEqual(LevelState.Won, s.State, $"level {n}: bot scored {s.Seat.Board.Score}, target {s.Definition.TargetScore}");
            Assert.AreEqual(3, s.Stars, $"level {n}: bot should earn 3 stars");
        }
    }

    [Test] public void DifficultyRises()
    {
        var easy = LevelLibrary.Get(1);
        var hard = LevelLibrary.Get(LevelLibrary.Count);
        Assert.Greater(hard.Obstacles.Length, easy.Obstacles.Length);
        Assert.Greater((double)hard.TargetScore / hard.ThreeStarScore, (double)easy.TargetScore / easy.ThreeStarScore);
    }

    [Test] public void ObstaclesNeverFillALine()
    {
        for (int n = 1; n <= LevelLibrary.Count; n++)
        {
            var obs = LevelLibrary.Get(n).Obstacles;
            for (int i = 0; i < 8; i++)
            {
                Assert.Less(obs.Count(o => o.y == i), 8);
                Assert.Less(obs.Count(o => o.x == i), 8);
            }
        }
    }

    [Test] public void LevelIsLostWhenTargetMissed()
    {
        var s = new LevelSession(1);
        // burn every move on the smallest legal placement without aiming for lines
        while (s.State == LevelState.Playing)
        {
            bool moved = false;
            for (int i = 0; i < 3 && !moved; i++)
                for (int x = 0; x < 8 && !moved; x++)
                    for (int y = 0; y < 8 && !moved; y++)
                        moved = s.TryPlace(i, x, y);
            if (!moved) break;
        }
        if (s.Seat.Board.Score < s.Definition.TargetScore) Assert.AreEqual(LevelState.Lost, s.State);
        Assert.IsFalse(s.TryPlace(0, 0, 0), "no moves after the level ends");
    }

    [Test] public void ProgressUnlocksAndRoundTrips()
    {
        var p = new Progress();
        Assert.IsTrue(p.IsUnlocked(1));
        Assert.IsFalse(p.IsUnlocked(2));
        p.Record(1, 2);
        p.Record(1, 1); // keeps best
        Assert.IsTrue(p.IsUnlocked(2));
        var q = Progress.Parse(p.Serialize());
        Assert.AreEqual(2, q.StarsFor(1));
        Assert.AreEqual(2, q.TotalStars);
        Assert.IsTrue(Progress.Parse("garbage!!").IsUnlocked(1));
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
