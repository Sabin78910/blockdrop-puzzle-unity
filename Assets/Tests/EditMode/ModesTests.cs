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

    // the 30 launch levels, two full generated chapters, and spot checks deep into the endless run
    private static readonly int[] Sample = Enumerable.Range(1, 50).Concat(new[] { 99, 100, 101, 555, 1000, 4999, 10000, 99999, 100000, 543210, LevelLibrary.Count }).ToArray();

    [Test] public void EveryLevelIsBeatableByTheBot()
    {
        foreach (int n in Sample)
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
        var hard = LevelLibrary.Get(LevelLibrary.Handmade);
        Assert.Greater(hard.Obstacles.Length, easy.Obstacles.Length);
        Assert.Greater((double)hard.TargetScore / hard.ThreeStarScore, (double)easy.TargetScore / easy.ThreeStarScore);
    }

    [Test] public void GeneratedLevelsFollowASawtoothThatRisesOverall()
    {
        Assert.AreEqual(LevelTier.Normal, LevelLibrary.Get(31).Tier);
        Assert.AreEqual(LevelTier.Hard, LevelLibrary.Get(35).Tier);
        Assert.AreEqual(LevelTier.SuperHard, LevelLibrary.Get(40).Tier);
        Assert.AreEqual(LevelTier.Normal, LevelLibrary.Get(30).Tier, "launch levels are not relabelled");
        double Ratio(int n) { var d = LevelLibrary.Get(n); return (double)d.TargetScore / d.ThreeStarScore; }
        double Chapter(int first) => Enumerable.Range(first, 10).Select(n => (double)LevelLibrary.Get(n).Obstacles.Length).Average();
        Assert.Greater(Chapter(2001), Chapter(31), "deep chapters have more obstacles on average");
        Assert.Greater(Ratio(40), Ratio(41), "a breather follows the super hard level");
        Assert.LessOrEqual(LevelLibrary.Get(LevelLibrary.Count).Obstacles.Length, 22);
    }

    [Test] public void GeneratedLevelsDifferFromEachOther()
    {
        var layouts = Enumerable.Range(31, 100).Select(n => string.Join(";", LevelLibrary.Get(n).Obstacles)).Distinct().Count();
        Assert.AreEqual(100, layouts);
        Assert.Throws<System.ArgumentOutOfRangeException>(() => LevelLibrary.Get(LevelLibrary.Count + 1));
    }

    [Test] public void ObstaclesNeverFillALine()
    {
        foreach (int n in Sample)
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

    [Test] public void ProgressGrowsPastTheLaunchLevelsAndKeepsOldSaves()
    {
        var old = Progress.Parse(new string('3', 30)); // a 1.0.0 save with every launch level beaten
        Assert.AreEqual(90, old.TotalStars);
        Assert.IsTrue(old.IsUnlocked(31));
        Assert.AreEqual(31, old.Frontier);
        for (int n = 31; n <= 5000; n++) old.Record(n, 1);
        var q = Progress.Parse(old.Serialize());
        Assert.AreEqual(90 + 4970, q.TotalStars);
        Assert.IsTrue(q.IsUnlocked(5001));
        Assert.IsFalse(q.IsUnlocked(5002));
        Assert.AreEqual(5001, q.Frontier);
        Assert.AreEqual(5000, q.Serialize().Length);
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
