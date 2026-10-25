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

public class ThemeAndStoreTests
{
    [Test] public void ThemeCatalogIsSound()
    {
        // saves store owned themes as a bit mask by index: the first five must never move
        CollectionAssert.AreEqual(new[] { "Jewel", "Neon", "Candy", "Ocean", "Sunset" }, PlayerMeta.Themes.Take(5).Select(t => t.Name));
        Assert.LessOrEqual(PlayerMeta.Themes.Length, 31);
        foreach (var t in PlayerMeta.Themes)
        {
            Assert.AreEqual(5, t.Blocks.Length, t.Name);
            foreach (var hex in t.Blocks.Concat(new[] { t.BackgroundTint, t.Panel, t.Slot }))
                Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(hex, "^[0-9A-F]{6}$"), $"{t.Name}: {hex}");
            if (t.Tier == ThemeTier.Exclusive) Assert.IsTrue(t.ChapterReward > 0 || t.StarsReward > 0, t.Name + " says how to earn it");
            else Assert.AreEqual(t.Tier == ThemeTier.Starter, t.Price <= 500, t.Name + " price fits its tier");
        }
        Assert.That(PlayerMeta.Themes.Where(t => t.Tier == ThemeTier.Legendary).Min(t => t.Price), Is.GreaterThan(PlayerMeta.Themes.Where(t => t.Tier == ThemeTier.Premium).Max(t => t.Price)));
    }

    [Test] public void ExclusiveThemesAreEarnedNotBought()
    {
        int glacier = PlayerMeta.FindTheme("Glacier");
        var m = PlayerMeta.Parse("c=999999");
        Assert.IsFalse(m.BuyOrSelect(glacier), "no amount of coins buys an exclusive theme");
        Assert.IsEmpty(m.UnlockEarnedThemes(4, 100));
        CollectionAssert.AreEqual(new[] { glacier }, m.UnlockEarnedThemes(5, 100));
        Assert.IsTrue(m.BuyOrSelect(glacier), "owned now, so it can be used");
        Assert.AreEqual(999999, m.Coins);
        Assert.Contains(PlayerMeta.FindTheme("Starlight"), m.UnlockEarnedThemes(5, 250));
        Assert.IsTrue(PlayerMeta.Parse(m.Serialize()).Owns(glacier));
    }

    [Test] public void CoinPacksStackAndNonConsumablesGrantOnce()
    {
        var m = new PlayerMeta();
        var pack = Store.Find("coins_3500");
        Assert.IsTrue(m.Grant(pack));
        Assert.IsTrue(m.Grant(pack));
        Assert.AreEqual(7000, m.Coins);
        var royal = Store.Find("bundle_royal");
        Assert.IsTrue(m.Grant(royal));
        Assert.IsFalse(m.Grant(royal), "a restore never pays out twice");
        Assert.AreEqual(9000, m.Coins);
        Assert.IsTrue(m.Owns(PlayerMeta.FindTheme("Royal Gold")));
        Assert.IsTrue(m.NoAds);
        var back = PlayerMeta.Parse(m.Serialize());
        Assert.IsTrue(back.NoAds);
        Assert.IsFalse(back.Grant(royal));
    }

    [Test] public void EveryProductSaysWhatIsInside()
    {
        Assert.AreEqual(Store.Products.Length, Store.Products.Select(p => p.Id).Distinct().Count());
        foreach (var p in Store.Products)
        {
            Assert.IsTrue(p.Coins > 0 || p.Themes.Length > 0 || p.RemoveAds || p.Hammers > 0 || p.Shuffles > 0, p.Id);
            Assert.IsTrue(p.Themes.All(t => PlayerMeta.FindTheme(t) >= 0), p.Id + " themes exist");
            Assert.IsTrue(p.Themes.All(t => PlayerMeta.Themes[PlayerMeta.FindTheme(t)].Buyable), p.Id + " never sells an earned theme");
            Assert.IsNotEmpty(Store.Contents(p));
        }
        Assert.AreEqual("3,500 ●", Store.Contents(Store.Find("coins_3500")));
        Assert.IsFalse(new PlayerMeta().Grant(new Product { Id = "fake", Coins = 1000000 }), "only catalog products");
    }

    [Test] public void SpendNeedsEnoughCoins()
    {
        var m = PlayerMeta.Parse("c=150");
        Assert.IsFalse(m.Spend(200));
        Assert.IsTrue(m.Spend(100));
        Assert.AreEqual(50, m.Coins);
        Assert.IsFalse(m.Spend(-5));
    }
}
