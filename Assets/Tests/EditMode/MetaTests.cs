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

    [Test] public void XpLevelsUpAndGrantsCoins()
    {
        var m = new PlayerMeta();
        Assert.AreEqual(0, m.AddXp(PlayerMeta.XpToNext(1) - 1));
        Assert.AreEqual(1, m.Level);
        Assert.AreEqual(1, m.AddXp(1));
        Assert.AreEqual(2, m.Level);
        Assert.AreEqual(0, m.Xp);
        Assert.AreEqual(PlayerMeta.LevelReward(2), m.Coins);
        Assert.AreEqual(2, m.AddXp(PlayerMeta.XpToNext(2) + PlayerMeta.XpToNext(3)), "big score can give several levels");
        Assert.AreEqual(0, m.AddXp(-50));
    }

    [Test] public void AchievementsUnlockOnceAndPayCoins()
    {
        var m = new PlayerMeta();
        var first = m.Record(Stat.LinesCleared, 1);
        Assert.AreEqual("First Clear", first.Single().Name);
        Assert.AreEqual(10, m.Coins);
        Assert.IsEmpty(m.Record(Stat.LinesCleared, 1), "no repeat unlock");
        Assert.AreEqual("Line Cleaner", m.Record(Stat.LinesCleared, 98).Single().Name);
        Assert.IsEmpty(m.Record(Stat.BestCombo, 2));
        m.Record(Stat.BestCombo, 1);
        Assert.AreEqual(2, m.Stats[(int)Stat.BestCombo], "best stats keep the maximum");
    }

    [Test] public void LevelStatsAndAchievementsPersist()
    {
        var m = new PlayerMeta();
        m.AddXp(1234);
        m.Record(Stat.HardAiWins, 1);
        m.Record(Stat.GamesPlayed, 3);
        var back = PlayerMeta.Parse(m.Serialize());
        Assert.AreEqual(m.Level, back.Level);
        Assert.AreEqual(m.Xp, back.Xp);
        Assert.AreEqual(m.Coins, back.Coins);
        Assert.IsTrue(back.HasAchievement(10));
        Assert.AreEqual(3, back.Stats[(int)Stat.GamesPlayed]);
        Assert.AreEqual(1, PlayerMeta.Parse("c=5").Level, "old saves start at level 1");
    }
}
