using System.Linq;
using BlockDrop.Core;
using NUnit.Framework;

public class MetaTests
{
    [Test] public void StreakGrowsOnConsecutiveDaysAndResetsAfterAGap()
    {
        var m = new PlayerMeta();
        Assert.AreEqual(PlayerMeta.StreakReward(1), m.CheckIn("2026-10-09"));
        Assert.AreEqual(0, m.CheckIn("2026-10-09"), "only once per day");
        m.CheckIn("2026-10-10");
        m.CheckIn("2026-10-11");
        Assert.AreEqual(3, m.Streak);
        m.CheckIn("2026-10-13");
        Assert.AreEqual(1, m.Streak, "missed a day resets");
        Assert.AreEqual(3, m.BestStreak);
    }

    [Test] public void SeventhDayGivesBonus()
    {
        Assert.Greater(PlayerMeta.StreakReward(7), PlayerMeta.StreakReward(6) + 50);
    }

    [Test] public void MissionsAreDeterministicDistinctAndRollDaily()
    {
        var a = PlayerMeta.MissionsFor("2026-10-09");
        var b = PlayerMeta.MissionsFor("2026-10-09");
        CollectionAssert.AreEqual(a.Select(x => x.Text), b.Select(x => x.Text));
        Assert.AreEqual(3, a.Select(x => x.Kind).Distinct().Count());
    }

    [Test] public void TrackingCompletesAndClaimsOnce()
    {
        var m = new PlayerMeta();
        const string day = "2026-10-09";
        var missions = PlayerMeta.MissionsFor(day);
        for (int i = 0; i < 3; i++) m.Track(missions[i].Kind, missions[i].Target, day);
        int before = m.Coins;
        Assert.IsTrue(m.Claim(0, day));
        Assert.IsFalse(m.Claim(0, day), "no double claim");
        Assert.AreEqual(before + missions[0].Reward, m.Coins);
        m.Track(missions[1].Kind, 0, "2026-10-10");
        Assert.IsFalse(m.Claimed[0], "new day resets missions");
    }

    [Test] public void ComboMissionTracksBestNotSum()
    {
        var m = new PlayerMeta();
        string day = Enumerable.Range(1, 60).Select(d => $"2026-11-{(d % 28) + 1:00}")
            .First(d => PlayerMeta.MissionsFor(d).Any(x => x.Kind == MissionKind.ComboReach));
        int idx = System.Array.FindIndex(PlayerMeta.MissionsFor(day), x => x.Kind == MissionKind.ComboReach);
        m.Track(MissionKind.ComboReach, 1, day);
        m.Track(MissionKind.ComboReach, 1, day);
        Assert.AreEqual(1, m.Progress[idx]);
    }

    [Test] public void ThemesCostCoinsAndPersist()
    {
        var m = PlayerMeta.Parse("c=200");
        Assert.IsTrue(m.Owns(0));
        Assert.IsTrue(m.BuyOrSelect(1)); // Neon 150
        Assert.AreEqual(50, m.Coins);
        Assert.IsFalse(m.BuyOrSelect(4), "too expensive");
        var back = PlayerMeta.Parse(m.Serialize());
        Assert.IsTrue(back.Owns(1));
        Assert.AreEqual(1, back.ThemeIndex);
        Assert.AreEqual(50, back.Coins);
        Assert.AreEqual(0, PlayerMeta.Parse("t=3").ThemeIndex, "can't select an unowned theme");
    }
}
