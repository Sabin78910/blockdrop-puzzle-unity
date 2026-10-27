using BlockDrop.Core;
using NUnit.Framework;

public class RankTests
{
    [Test] public void LadderRunsRookieToLegendInOrder()
    {
        foreach (RankTrack t in System.Enum.GetValues(typeof(RankTrack)))
        {
            int last = -1;
            for (int s = 0; s <= Ranks.TopStep; s++)
            {
                Assert.Greater(Ranks.PointsFor(t, s), last);
                last = Ranks.PointsFor(t, s);
                Assert.AreEqual(s, Ranks.StepOf(t, last));
            }
            Assert.AreEqual(Ranks.TopStep, Ranks.StepOf(t, int.MaxValue / 2));
        }
        Assert.AreEqual("ROOKIE III", Ranks.Name(0));
        Assert.AreEqual("GOLD II", Ranks.Name(10));
        Assert.AreEqual("LEGEND", Ranks.Name(Ranks.TopStep));
    }

    [Test] public void FillAndToNextTrackProgress()
    {
        Assert.AreEqual(0f, Ranks.Fill(RankTrack.Classic, 0), 1e-4);
        Assert.AreEqual(0.5f, Ranks.Fill(RankTrack.Classic, 15), 1e-4);
        Assert.AreEqual(15, Ranks.ToNext(RankTrack.Classic, 15));
        Assert.AreEqual(1f, Ranks.Fill(RankTrack.Classic, int.MaxValue / 2));
        Assert.AreEqual(0, Ranks.ToNext(RankTrack.Classic, int.MaxValue / 2));
    }

    [Test] public void FirstClassicGameRanksUpAndPaysOnce()
    {
        var m = new PlayerMeta();
        int coins = m.Coins;
        var r = m.RankClassic(800);
        Assert.IsTrue(r.Promoted);
        Assert.Greater(r.Coins, 0);
        Assert.AreEqual(coins + r.Coins, m.Coins);
        Assert.Greater(r.Delta, 0);
    }

    [Test] public void NewTierGivesBoosters()
    {
        var m = new PlayerMeta();
        int hammers = m.BoostersOf(Booster.Hammer);
        var r = m.AwardRank(RankTrack.Classic, Ranks.PointsFor(RankTrack.Classic, 3));
        Assert.AreEqual(3, r.StepAfter);
        Assert.AreEqual(1, r.Hammers);
        Assert.AreEqual(hammers + 1, m.BoostersOf(Booster.Hammer));
    }

    [Test] public void DailyCountsOncePerDay()
    {
        var m = new PlayerMeta();
        var a = m.RankDaily(1000, "2026-10-27");
        var b = m.RankDaily(5000, "2026-10-27");
        Assert.Greater(a.Delta, 0);
        Assert.IsTrue(b.AlreadyScored);
        Assert.AreEqual(0, b.Delta);
        Assert.Greater(m.RankDaily(1000, "2026-10-28").Delta, 0);
    }

    [Test] public void VsLossNeverDropsBelowTierStartAndRewardsPayOnce()
    {
        var m = new PlayerMeta();
        m.AwardRank(RankTrack.VsAi, Ranks.PointsFor(RankTrack.VsAi, 4) + 1); // Bronze III, tier start is step 3
        int floor = Ranks.PointsFor(RankTrack.VsAi, 3);
        for (int i = 0; i < 40; i++) m.RankVs(BotLevel.Hard, 1);
        Assert.AreEqual(floor, m.RankPoints[(int)RankTrack.VsAi]);
        int coins = m.Coins;
        m.RankVs(BotLevel.Hard, 0); m.RankVs(BotLevel.Hard, 1);
        var back = m.AwardRank(RankTrack.VsAi, 0);
        Assert.AreEqual(0, back.Coins);
        Assert.GreaterOrEqual(m.Coins, coins);
    }

    [Test] public void EasyAiStopsPayingAtItsCapButStillCostsOnLoss()
    {
        var m = new PlayerMeta();
        m.AwardRank(RankTrack.VsAi, Ranks.EasyCap);
        var win = m.RankVs(BotLevel.Easy, 0);
        Assert.IsTrue(win.Capped);
        Assert.AreEqual(0, win.Delta);
        Assert.Less(m.RankVs(BotLevel.Easy, 1).Delta, 0);
        Assert.Greater(m.RankVs(BotLevel.Hard, 0).Delta, 0);
    }

    [Test] public void WinStreakAddsBonusAndLossResetsIt()
    {
        var m = new PlayerMeta();
        int first = m.RankVs(BotLevel.Medium, 0).Delta;
        int second = m.RankVs(BotLevel.Medium, 0).Delta;
        Assert.Greater(second, first);
        m.RankVs(BotLevel.Medium, 1);
        Assert.AreEqual(0, m.AiWinStreak);
        Assert.AreEqual(0, m.RankVs(BotLevel.Medium, -1).Delta, "a tie changes nothing");
    }

    [Test] public void RanksSurviveSaveAndMerge()
    {
        var a = new PlayerMeta();
        a.RankClassic(3000); a.RankDaily(900, "2026-10-27"); a.RankVs(BotLevel.Hard, 0);
        var b = PlayerMeta.Parse(a.Serialize());
        for (int i = 0; i < 3; i++) { Assert.AreEqual(a.RankPoints[i], b.RankPoints[i]); Assert.AreEqual(a.RankPeak[i], b.RankPeak[i]); }
        Assert.AreEqual(a.DailyRankDay, b.DailyRankDay);
        Assert.AreEqual(a.AiWinStreak, b.AiWinStreak);
        var other = new PlayerMeta();
        other.AwardRank(RankTrack.Classic, 5000);
        var merged = PlayerMeta.Merge(a, other);
        Assert.AreEqual(5000, merged.RankPoints[(int)RankTrack.Classic]);
        Assert.AreEqual(a.RankPoints[(int)RankTrack.VsAi], merged.RankPoints[(int)RankTrack.VsAi]);
    }
}
