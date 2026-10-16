using System.Linq;
using BlockDrop.Core;
using NUnit.Framework;

public class FeelTests
{
    [Test] public void LadderScalesWithAchievement()
    {
        Assert.AreEqual(FeedbackTier.None, Feel.TierFor(0, 0, false));
        Assert.AreEqual(FeedbackTier.Line, Feel.TierFor(1, 1, false));
        Assert.AreEqual(FeedbackTier.Great, Feel.TierFor(2, 1, false));
        Assert.AreEqual(FeedbackTier.Great, Feel.TierFor(1, 2, false));
        Assert.AreEqual(FeedbackTier.Excellent, Feel.TierFor(3, 1, false));
        Assert.AreEqual(FeedbackTier.Amazing, Feel.TierFor(4, 1, false));
        Assert.AreEqual(FeedbackTier.Amazing, Feel.TierFor(1, 4, false));
        Assert.AreEqual(FeedbackTier.BoardClear, Feel.TierFor(1, 1, true));
    }

    [Test] public void SingleLineGetsNoPraiseAndPraiseIsTranslated()
    {
        Assert.IsNull(Feel.Praise(FeedbackTier.Line));
        Assert.IsNull(Feel.Praise(FeedbackTier.None));
        foreach (var t in new[] { FeedbackTier.Great, FeedbackTier.Excellent, FeedbackTier.Amazing, FeedbackTier.BoardClear })
            Assert.IsTrue(Loc.Table.ContainsKey(Feel.Praise(t)), t.ToString());
    }

    [Test] public void OnlyBigMomentsShakeAndShakeIsCapped()
    {
        Assert.AreEqual(0f, Feel.Shake(FeedbackTier.Line, 1, false));
        Assert.AreEqual(0f, Feel.Shake(FeedbackTier.Great, 2, false));
        Assert.Greater(Feel.Shake(FeedbackTier.Excellent, 3, false), 0f);
        Assert.LessOrEqual(Feel.Shake(FeedbackTier.Amazing, 8, false), 0.4f);
        Assert.IsFalse(Feel.SlowMotion(FeedbackTier.Excellent, false));
        Assert.IsTrue(Feel.SlowMotion(FeedbackTier.Amazing, false));
        Assert.AreEqual(40, Feel.Confetti(FeedbackTier.BoardClear, false));
        Assert.AreEqual(0, Feel.Confetti(FeedbackTier.Amazing, false));
    }

    [Test] public void ReducedMotionDropsMovementEverywhere()
    {
        foreach (FeedbackTier t in System.Enum.GetValues(typeof(FeedbackTier)))
        {
            Assert.AreEqual(0f, Feel.Shake(t, 4, true));
            Assert.IsFalse(Feel.SlowMotion(t, true));
            Assert.AreEqual(0, Feel.Confetti(t, true));
        }
        Assert.AreEqual(0f, Feel.HitStop(3, true));
        Assert.Greater(Feel.HitStop(3, false), 0f);
        // the information stays: praise does not depend on motion
        Assert.AreEqual("AMAZING!", Feel.Praise(FeedbackTier.Amazing));
    }

    [Test] public void ReducedMotionFollowsPhoneUntilPlayerChooses()
    {
        Assert.IsTrue(Feel.ReducedMotion(-1, 0f));
        Assert.IsFalse(Feel.ReducedMotion(-1, 1f));
        Assert.IsFalse(Feel.ReducedMotion(0, 0f));
        Assert.IsTrue(Feel.ReducedMotion(1, 1f));
    }

    [Test] public void NewLookIsOffUntilChosen()
    {
        Assert.AreEqual(Look.NewLookDefault, Look.NewLook(-1));
        Assert.IsTrue(Look.NewLook(1));
        Assert.IsFalse(Look.NewLook(0));
    }

    [Test] public void NearMissFindsTheCellThatWouldClearMost()
    {
        var b = new Board(8);
        for (int x = 0; x < 8; x++) if (x != 5) b.SetCell(x, 2, 0);   // row 2 missing (5,2)
        for (int y = 0; y < 8; y++) if (y != 2) b.SetCell(5, y, 1);   // column 5 missing (5,2)
        var m = NearMiss.Find(b);
        Assert.IsTrue(m.Found);
        Assert.AreEqual((5, 2, 2), (m.X, m.Y, m.Lines));
        Assert.AreEqual("One cell away from a 2-line clear!", m.Message());
    }

    [Test] public void NearMissIsHonestWhenNothingWasClose()
    {
        var b = new Board(8);
        b.SetCell(0, 0, 0);
        var m = NearMiss.Find(b);
        Assert.IsFalse(m.Found);
        Assert.IsNull(m.Message());

        for (int x = 0; x < 7; x++) b.SetCell(x, 7, 0);
        var one = NearMiss.Find(b);
        Assert.AreEqual((7, 7, 1), (one.X, one.Y, one.Lines));
        Assert.AreEqual("One cell away from a clear!", one.Message());
    }

    [Test] public void NewLookStringsAreTranslated()
    {
        foreach (var k in new[] { "Fair hands. Every game.", "One cell away from a {0}-line clear!", "One cell away from a clear!",
                                  "MOTION: FULL", "MOTION: REDUCED", "NEW LOOK ON", "NEW LOOK OFF" })
            Assert.IsTrue(Loc.Table.ContainsKey(k), k);
    }

    [Test] public void EmptyBoardCheck()
    {
        var b = new Board(8);
        Assert.IsTrue(Feel.IsEmpty(b));
        b.SetCell(3, 3, 2);
        Assert.IsFalse(Feel.IsEmpty(b));
    }
}
