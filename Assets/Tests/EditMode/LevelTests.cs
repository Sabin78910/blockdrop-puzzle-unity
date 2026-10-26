using System.Linq;
using BlockDrop.Core;
using NUnit.Framework;

public class LevelTests
{
    private static LevelSession PlayOut(int number, int seed, int bonus = 0)
    {
        var s = new LevelSession(number, bonus);
        var rng = new System.Random(seed);
        while (s.State == LevelState.Playing && Playtester.TryMove(s.Seat.Board, s.Seat.Dealer.Hand, rng, out int i, out int x, out int y))
            Assert.IsTrue(s.TryPlace(i, x, y), $"level {number}: playtester move rejected");
        return s;
    }

    [Test] public void BakedTableMatchesTheTuner()
    {
        // the phone reads LevelTable; it must be what the playtester tuning produces (run tools/LevelTuner)
        foreach (int n in new[] { 1, 14, 39, 45, 77, 100 })
        {
            var baked = LevelLibrary.Get(n);
            var tuned = Playtester.Tune(n).Definition;
            Assert.AreEqual(tuned.MaxMoves, baked.MaxMoves, $"level {n} moves");
            Assert.AreEqual(tuned.TwoStarScore, baked.TwoStarScore, $"level {n} two stars");
            Assert.AreEqual(tuned.ThreeStarScore, baked.ThreeStarScore, $"level {n} three stars");
            CollectionAssert.AreEqual(tuned.Blockers, baked.Blockers, $"level {n} blockers");
            CollectionAssert.AreEqual(tuned.Goals.Select(g => (g.Kind, g.Target)), baked.Goals.Select(g => (g.Kind, g.Target)), $"level {n} goals");
        }
    }

    [Test] public void EveryLevelIsWellFormed()
    {
        Assert.AreEqual(LevelLibrary.Count, LevelLibrary.Chapters.Length * LevelLibrary.PerChapter);
        for (int n = 1; n <= LevelLibrary.Count; n++)
        {
            var d = LevelLibrary.Get(n);
            Assert.AreSame(d, LevelLibrary.Get(n), "cached");
            Assert.IsNotEmpty(d.Goals, $"level {n}");
            Assert.IsTrue(d.Goals.All(g => g.Target > 0), $"level {n}: every goal has something to do");
            Assert.That(d.MaxMoves, Is.InRange(8, Playtester.MoveCap), $"level {n} moves");
            Assert.LessOrEqual(d.MaxMoves, Playtester.LongestLevel(d.Tier) + 6, $"level {n} is snack sized");
            Assert.Greater(d.ThreeStarScore, d.TwoStarScore, $"level {n} stars");
            for (int i = 0; i < 8; i++)
            {
                Assert.LessOrEqual(d.Blockers.Count(b => b.y == i), 5, $"level {n} row {i}");
                Assert.LessOrEqual(d.Blockers.Count(b => b.x == i), 5, $"level {n} column {i}");
            }
            if (LevelLibrary.RulesFor(n).Goals.Contains(GoalKind.Gems)) Assert.GreaterOrEqual(d.Blockers.Count(b => b.cell == Board.Gem), 2, $"level {n} gems");
        }
    }

    [Test] public void SomethingNewTurnsUpEveryFewLevelsAndTheBossIsAnnounced()
    {
        for (int c = 0; c < LevelLibrary.Chapters.Length; c++)
        {
            int first = c * LevelLibrary.PerChapter + 1, gap = 0, worst = 0;
            for (int k = 0; k < LevelLibrary.PerChapter; k++)
            {
                gap = LevelLibrary.Get(first + k).Intro != null ? 0 : gap + 1;
                worst = System.Math.Max(worst, gap);
            }
            Assert.LessOrEqual(worst, 3, $"chapter {c + 1} has a new beat at least every four levels");
            StringAssert.Contains("BOSS", LevelLibrary.Get(first + 8).Intro);
        }
        Assert.IsNull(LevelLibrary.Beat(0));
        Assert.AreEqual(0, LevelLibrary.BossBonus(8));
        Assert.Greater(LevelLibrary.BossBonus(9), 0);
        Assert.Greater(LevelLibrary.BossBonus(99), LevelLibrary.BossBonus(9));
        Assert.AreEqual(0, LevelLibrary.BossBonus(109));
    }

    [Test] public void TheLevelPreviewGestureIsOnlyInDevelopmentBuilds()
    {
        string src = System.IO.File.ReadAllText("Assets/Scripts/Game/GameController.cs");
        Assert.IsTrue(src.Contains("Debug.isDebugBuild && GUI.Button"), "the five-tap preview needs the development-build gate");
        Assert.IsTrue(src.Contains("(Debug.isDebugBuild && _devPreview)"), "unlocking levels needs the gate too");
    }

    [Test] public void TwistLevelsBringIceAndGemsIntoTheFirstTwentyLevels()
    {
        Assert.IsTrue(LevelLibrary.IsTwist(6) && LevelLibrary.IsTwist(16) && !LevelLibrary.IsTwist(7));
        Assert.Contains(GoalKind.Gems, LevelLibrary.RulesFor(6).Goals);
        Assert.Contains(GoalKind.Stones, LevelLibrary.RulesFor(16).Goals);
        Assert.AreEqual(Board.Ice, LevelLibrary.RulesFor(16).Special);
        Assert.IsTrue(LevelLibrary.Get(6).Blockers.Any(b => b.cell == Board.Gem), "gems by level 6");
        Assert.IsTrue(LevelLibrary.Get(16).Blockers.Any(b => b.cell == Board.Ice), "ice by level 16");
        for (int c = 0; c < LevelLibrary.Chapters.Length; c++)
        {
            int n = c * LevelLibrary.PerChapter + 6;
            Assert.IsNotNull(LevelLibrary.Get(n).Intro, $"twist level {n} says what is new");
            Assert.AreNotSame(LevelLibrary.Chapters[c], LevelLibrary.RulesFor(n), $"twist level {n} borrows another chapter's rules");
        }
        foreach (var shape in LevelLibrary.TwistLayouts)
        {
            Assert.AreEqual(8, shape.Length);
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(8, shape[i].Length);
                Assert.LessOrEqual(shape[i].Count(ch => ch != '.'), 5, "row " + i);
                Assert.LessOrEqual(shape.Count(r => r[i] != '.'), 5, "column " + i);
            }
        }
    }

    [Test] public void TurnedShapesKeepTheirBlocksAndNoPictureRepeats()
    {
        foreach (var shape in LevelLibrary.Layouts.Concat(LevelLibrary.TwistLayouts))
            for (int pose = 0; pose < 8; pose++)
            {
                var turned = LevelLibrary.Orient(shape, pose);
                Assert.AreEqual(shape.Sum(r => r.Count(c => c == '#')), turned.Sum(r => r.Count(c => c == '#')), "stones kept");
                Assert.AreEqual(shape.Sum(r => r.Count(c => c == '*')), turned.Sum(r => r.Count(c => c == '*')), "specials kept");
                for (int i = 0; i < 8; i++)
                {
                    Assert.LessOrEqual(turned[i].Count(c => c != '.'), 5, "row " + i);
                    Assert.LessOrEqual(turned.Count(r => r[i] != '.'), 5, "column " + i);
                }
            }
        // no two levels may open on the same board, so every level looks new
        var seen = new System.Collections.Generic.HashSet<string>();
        int repeats = 0;
        for (int n = 1; n <= LevelLibrary.Count; n++)
            if (!seen.Add(string.Join(";", LevelLibrary.Get(n).Blockers))) repeats++;
        Assert.LessOrEqual(repeats, 3, "only a fully symmetric drawing may come back as the same board");
    }

    [Test] public void EachChapterIntroducesSomethingAndDifficultyIsASawtooth()
    {
        for (int c = 0; c < LevelLibrary.Chapters.Length; c++)
        {
            int first = c * LevelLibrary.PerChapter + 1;
            Assert.IsNotNull(LevelLibrary.Get(first).Intro, $"chapter {c + 1} explains its new idea");
            Assert.IsNull(LevelLibrary.Get(first + 1).Intro);
            // every spike is followed by relief, and a chapter ends on a relief level
            for (int k = 0; k < LevelLibrary.PerChapter - 1; k++)
            {
                int n = first + k;
                if (LevelLibrary.TierFor(n) == LevelTier.SuperHard || (LevelLibrary.TierFor(n) == LevelTier.Hard && LevelLibrary.TierFor(n + 1) != LevelTier.SuperHard))
                    Assert.AreEqual(LevelTier.Relief, LevelLibrary.TierFor(n + 1), $"level {n + 1} after a spike");
            }
            Assert.AreEqual(LevelTier.Relief, LevelLibrary.TierFor(first + LevelLibrary.PerChapter - 1));
            Assert.Less(LevelLibrary.TargetWinRate(first + 8), LevelLibrary.TargetWinRate(first + 9), "relief is easier than the boss");
        }
        Assert.GreaterOrEqual(LevelLibrary.TargetWinRate(LevelLibrary.Count - 1), 0.40, "hard should feel like almost, never a wall");
        // the old curve became a wall after level 26: now the late chapters are tuned like the early ones
        Assert.AreEqual(LevelTier.Relief, LevelLibrary.TierFor(30));
    }

    [Test] public void EveryLevelIsWinnableByASimulatedPlayer()
    {
        for (int n = 1; n <= LevelLibrary.Count; n++)
        {
            bool won = Enumerable.Range(0, 12).Any(seed => PlayOut(n, seed).State == LevelState.Won);
            Assert.IsTrue(won, $"level {n}: no simulated player won in 12 tries");
        }
    }

    [Test] public void WinsTheMomentGoalsAreMetAndSpareMovesBecomeBonus()
    {
        var s = PlayOut(1, 3);
        Assert.AreEqual(LevelState.Won, s.State);
        Assert.IsTrue(s.AllGoalsMet);
        Assert.AreEqual(s.MovesLeft * LevelLibrary.MoveBonus, s.Bonus);
        Assert.AreEqual(s.Seat.Board.Score + s.Bonus, s.FinalScore);
        Assert.That(s.Stars, Is.InRange(1, 3));
        Assert.IsFalse(s.TryPlace(0, 0, 0), "no moves after the level ends");
    }

    [Test] public void AStuckHandIsReshuffledSoOnlyMovesCanLoseALevel()
    {
        for (int n = 40; n <= 50; n++)
        {
            var s = PlayOut(n, 5);
            if (s.State == LevelState.Lost) Assert.AreEqual(0, s.MovesLeft, $"level {n} lost with moves left");
        }
    }

    [Test] public void LevelHandsAlwaysFitWhenDealt()
    {
        var def = LevelLibrary.Get(29); // a super-hard level full of stones
        var seat = LevelLibrary.NewSeat(def);
        Assert.IsTrue(seat.Dealer.Hand.All(p => p != null && seat.Board.CanPlaceAnywhere(p)), "first hand, dealt after the stones");
        var rng = new System.Random(2);
        for (int k = 0; k < 30 && !seat.Out; k++)
        {
            bool fresh = seat.Dealer.Hand.Count(p => p != null) == 1;
            Assert.IsTrue(Playtester.TryMove(seat.Board, seat.Dealer.Hand, rng, out int i, out int x, out int y));
            seat.TryPlace(i, x, y);
            if (fresh) Assert.IsTrue(seat.Dealer.Hand.All(p => seat.Board.CanPlaceAnywhere(p)), "every piece of a new hand fits");
        }
    }

    private static void RunOutOfMoves(LevelSession s, int lines)
    {
        s.Seat.Board.RestoreScore(0, 0, 0, lines);
        s.Seat.RestoreCounters(s.MaxMoves, 0);
    }

    [Test] public void LosingOffersTwoContinues()
    {
        var s = new LevelSession(11); // a lines level
        RunOutOfMoves(s, 0);
        Assert.AreEqual(LevelState.Lost, s.State);
        int before = s.MaxMoves;
        Assert.IsTrue(s.Continue());
        Assert.AreEqual(before + LevelSession.ContinueMoves, s.MaxMoves);
        Assert.AreEqual(LevelSession.ContinueMoves, s.MovesLeft);
        Assert.AreEqual(LevelState.Playing, s.State);
        RunOutOfMoves(s, 0);
        Assert.IsTrue(s.Continue());
        RunOutOfMoves(s, 0);
        Assert.IsFalse(s.CanContinue, "two continues at most");
        Assert.IsFalse(s.Continue());
    }

    [Test] public void NearMissOnlyWhenReallyClose()
    {
        var s = new LevelSession(11);
        var goal = s.Definition.Goals[0];
        Assert.AreEqual(GoalKind.Lines, goal.Kind);
        Assert.IsNull(s.NearMiss(), "nothing while playing");
        RunOutOfMoves(s, goal.Target - 1);
        Assert.AreEqual("So close! Just 1 more line.", s.NearMiss());
        var far = new LevelSession(11);
        RunOutOfMoves(far, 0);
        Assert.IsNull(far.NearMiss(), "never fake an almost");
    }

    [Test] public void HelpingHandAddsVisibleMoves()
    {
        var plain = new LevelSession(9);
        var helped = new LevelSession(9, 3);
        Assert.AreEqual(plain.MaxMoves + 3, helped.MaxMoves);
        Assert.AreEqual(3, helped.ExtraMoves);
    }

    [Test] public void ProgressRecordsNewStarsAndFrontier()
    {
        var p = new Progress();
        Assert.IsTrue(p.IsUnlocked(1));
        Assert.IsFalse(p.IsUnlocked(2));
        Assert.AreEqual(2, p.Record(1, 2));
        Assert.AreEqual(0, p.Record(1, 1), "keeps best, nothing new");
        Assert.AreEqual(1, p.Record(1, 3), "one new star");
        Assert.AreEqual(2, p.Frontier);
        var q = Progress.Parse(p.Serialize());
        Assert.AreEqual(3, q.StarsFor(1));
        Assert.AreEqual(LevelLibrary.Count, p.Serialize().Length);
        Assert.IsTrue(Progress.Parse("garbage!!").IsUnlocked(1));
        Assert.AreEqual(2, Progress.Parse("322").StarsFor(3), "old 30-level saves still load");
    }

    [Test] public void SavedLevelKeepsExtraMovesAndSpecialBlocks()
    {
        var level = new LevelSession(45, 3);
        level.Seat.Board.SetCell(0, 0, Board.Ice);
        level.Seat.Board.SetCell(1, 0, Board.Gem);
        var g = SavedGame.Parse(SavedGame.Capture("Level", level.Seat, false, 45, "2026-10-25", 2, level.ExtraMoves, 1).Serialize());
        Assert.IsNotNull(g);
        Assert.AreEqual(3, g.Extra);
        Assert.AreEqual(1, g.Continues);
        var resumed = new LevelSession(g.Level);
        g.ApplyTo(resumed.Seat);
        resumed.RestoreExtra(g.Extra, g.Continues);
        Assert.AreEqual(level.MaxMoves, resumed.MaxMoves);
        Assert.AreEqual(Board.Ice, resumed.Seat.Board[0, 0]);
        Assert.AreEqual(Board.Gem, resumed.Seat.Board[1, 0]);
    }
}

public class BoosterTests
{
    [Test] public void HammerBreaksOneBlockAndCracksIce()
    {
        var b = new Board(8);
        Assert.IsFalse(b.Smash(0, 0), "nothing to smash");
        Assert.IsFalse(b.Smash(-1, 9), "off the board");
        b.SetCell(1, 1, 3);
        b.SetCell(2, 2, Board.Ice);
        Assert.IsTrue(b.Smash(1, 1));
        Assert.IsFalse(b.IsFilled(1, 1));
        Assert.IsTrue(b.Smash(2, 2));
        Assert.AreEqual(Board.Stone, b[2, 2], "ice takes two hits");
        Assert.IsTrue(b.Smash(2, 2));
        Assert.IsFalse(b.IsFilled(2, 2));
        Assert.AreEqual(0, b.Score, "smashing never scores");
    }

    [Test] public void SmashingAGemCountsTowardTheGoalWithoutUsingAMove()
    {
        int n = Enumerable.Range(31, 10).First(k => LevelLibrary.Get(k).Goals.Any(g => g.Kind == GoalKind.Gems));
        var s = new LevelSession(n);
        var gem = s.Definition.Blockers.First(b => b.cell == Board.Gem);
        int goal = System.Array.FindIndex(s.Definition.Goals, g => g.Kind == GoalKind.Gems);
        int moves = s.MovesLeft;
        Assert.IsTrue(s.Smash(gem.x, gem.y));
        Assert.AreEqual(1, s.Progress(goal));
        Assert.AreEqual(moves, s.MovesLeft);
        Assert.AreEqual(1, s.BoostersUsed);
    }

    [Test] public void HammeringTheLastStoneWinsTheLevel()
    {
        int n = Enumerable.Range(21, 10).First(k => LevelLibrary.Get(k).Goals.Single().Kind == GoalKind.Stones);
        var s = new LevelSession(n);
        foreach (var b in s.Definition.Blockers)
            while (s.Seat.Board.IsFilled(b.x, b.y)) Assert.IsTrue(s.Smash(b.x, b.y));
        Assert.AreEqual(LevelState.Won, s.State);
        Assert.AreEqual(s.MaxMoves * LevelLibrary.MoveBonus, s.Bonus, "every move left becomes bonus");
        Assert.IsFalse(s.Smash(0, 0), "no boosters after the level ends");
    }

    [Test] public void ShuffleDealsAFittingHandForFree()
    {
        var s = new LevelSession(29);
        var before = s.Seat.Dealer.Hand.ToArray();
        int moves = s.MovesLeft;
        Assert.IsTrue(s.Shuffle());
        Assert.AreEqual(moves, s.MovesLeft);
        Assert.IsTrue(s.Seat.Dealer.Hand.All(p => p != null && s.Seat.Board.CanPlaceAnywhere(p)));
        Assert.IsFalse(before.SequenceEqual(s.Seat.Dealer.Hand), "new pieces");
    }

    [Test] public void BoostersAreGiftedEarnedBoughtAndSaved()
    {
        var fresh = PlayerMeta.Parse("c=0");
        Assert.AreEqual(PlayerMeta.StarterBoosters, fresh.BoostersOf(Booster.Hammer), "existing players get the starter gift too");
        Assert.IsTrue(fresh.UseBooster(Booster.Hammer));
        Assert.AreEqual(2, fresh.BoostersOf(Booster.Hammer));
        var empty = PlayerMeta.Parse("c=120;bo=0,0");
        Assert.IsFalse(empty.UseBooster(Booster.Shuffle));
        Assert.IsTrue(empty.BuyBooster(Booster.Shuffle));
        Assert.AreEqual(20, empty.Coins);
        Assert.IsFalse(empty.BuyBooster(Booster.Hammer), "150 coins needed");
        var back = PlayerMeta.Parse(empty.Serialize());
        Assert.AreEqual(1, back.BoostersOf(Booster.Shuffle));
        Assert.AreEqual(0, back.BoostersOf(Booster.Hammer));
        Assert.AreEqual(0, PlayerMeta.Parse("bo=-4,x").BoostersOf(Booster.Hammer));
    }

    [Test] public void EverySeventhStreakDayGivesAHammer()
    {
        var m = PlayerMeta.Parse("bo=0,0");
        for (int d = 1; d <= 7; d++) m.CheckIn($"2026-10-{d:00}");
        Assert.AreEqual(7, m.Streak);
        Assert.AreEqual(1, m.BoostersOf(Booster.Hammer));
    }

    [Test] public void BoosterBoxGrantsAndCatalogOrderIsStable()
    {
        // owned non-consumables are saved by position, so the catalog only ever grows at the end
        CollectionAssert.AreEqual(new[] { "coins_1200", "coins_3500", "coins_8000", "coins_20000", "remove_ads", "bundle_royal", "bundle_legends", "boosters_10" },
            Store.Products.Select(p => p.Id));
        var m = PlayerMeta.Parse("bo=0,0");
        Assert.IsTrue(m.Grant(Store.Find("boosters_10")));
        Assert.AreEqual(10, m.BoostersOf(Booster.Hammer));
        Assert.AreEqual(10, m.BoostersOf(Booster.Shuffle));
        Assert.AreEqual("10 hammers + 10 shuffles", Store.Contents(Store.Find("boosters_10")));
    }
}
