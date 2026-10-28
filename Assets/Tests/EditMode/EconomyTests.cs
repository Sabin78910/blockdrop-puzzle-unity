using System.Linq;
using BlockDrop.Core;
using NUnit.Framework;

public class EconomyTests
{
    [TestCase(0L, "0")]
    [TestCase(1125L, "1,125")]
    [TestCase(9999L, "9,999")]
    [TestCase(10000L, "10K")]
    [TestCase(12345L, "12.3K")]
    [TestCase(100000L, "100K")]
    [TestCase(999999L, "999K")]
    [TestCase(1000000L, "1M")]
    [TestCase(1150000L, "1.15M")]
    [TestCase(25000000L, "25M")]
    [TestCase(250000000L, "250M")]
    [TestCase(1000000000L, "1B")]
    [TestCase(1250000000L, "1.25B")]
    public void LargeNumbersAbbreviateWithoutRoundingUp(long n, string expected) => Assert.AreEqual(expected, Economy.Compact(n));

    [Test] public void AbbreviationsFitACoinPill()
    {
        foreach (long n in new long[] { 0, 99999, 999999, 99999999, 999999999, 123456789012 })
            Assert.LessOrEqual(Economy.Compact(n).Length, 7, n.ToString());
    }

    [Test] public void FortuneGrowsWithTheBestThemeOwned()
    {
        Assert.AreEqual(1.0, Economy.Fortune(0));
        Assert.AreEqual(1.0, Economy.Fortune(5000), "below the base nothing changes");
        Assert.AreEqual(1000000000 / 9000.0, Economy.Fortune(1000000000), 1e-6);
        var m = new PlayerMeta();
        Assert.AreEqual(1.0, m.Fortune);
        m.AddCoins(100);
        Assert.AreEqual(100, m.Coins);
        m.ApplyServer(0, new[] { "Aurora", "Royal Gold", "Ember" }, m.EarnedTotal, m.SpentOther);
        Assert.AreEqual(15000 / 9000.0, m.Fortune, 1e-9);
        long before = m.Coins;
        m.AddCoins(90);
        Assert.AreEqual(before + 150, m.Coins, "a 90 coin reward pays 150 at Fortune 1.67");
    }

    [Test] public void DaysToAffordRoundsUpAndNeverReturnsZeroForAGap()
    {
        Assert.AreEqual(0, Economy.DaysToAfford(0, 700));
        Assert.AreEqual(1, Economy.DaysToAfford(1, 700));
        Assert.AreEqual(2, Economy.DaysToAfford(701, 700));
        Assert.AreEqual(5, Economy.DaysToAfford(5, 0), "a zero rate is treated as 1 coin a day");
    }

    [Test] public void LadderPricesRiseAndReachOneBillion()
    {
        var sold = PlayerMeta.Themes.Where(t => t.ServerSold).ToArray();
        Assert.AreEqual(1000000000, sold.Max(t => t.Price));
        long last = 0;
        foreach (var tier in new[] { ThemeTier.Legendary, ThemeTier.Mythic, ThemeTier.Cosmic, ThemeTier.Eternal })
        {
            var prices = PlayerMeta.Themes.Where(t => t.Tier == tier).Select(t => t.Price).ToArray();
            Assert.IsNotEmpty(prices, tier.ToString());
            Assert.Greater(prices.Min(), last, tier + " starts above the tier before");
            CollectionAssert.AreEqual(prices.OrderBy(p => p).ToArray(), prices, tier + " is listed cheapest first");
            last = prices.Max();
        }
        Assert.AreEqual(100000, PlayerMeta.Themes.Single(t => t.Name == "Orchid").Price);
        Assert.Less(PlayerMeta.Themes.Length, 63, "owned themes are a 64 bit mask");
    }

    [Test] public void HighTiersOpenOneAtATime()
    {
        var m = new PlayerMeta();
        int ember = PlayerMeta.FindTheme("Ember"), nebula = PlayerMeta.FindTheme("Nebula"), sing = PlayerMeta.FindTheme("Singularity"), aurora = PlayerMeta.FindTheme("Aurora");
        Assert.IsNull(m.LockReason(aurora));
        Assert.IsNotNull(m.LockReason(ember));
        Assert.IsNotNull(m.LockReason(nebula));
        m.ApplyServer(0, new[] { "Aurora" }, 0, 0);
        Assert.IsNull(m.LockReason(ember));
        Assert.IsNotNull(m.LockReason(nebula));
        m.ApplyServer(0, new[] { "Aurora", "Ember", "Pulsar", "Aether" }, 0, 0);
        Assert.IsNotNull(m.LockReason(sing), "Singularity also needs Genesis");
        m.ApplyServer(0, new[] { "Aurora", "Ember", "Pulsar", "Aether", "Genesis" }, 0, 0);
        Assert.IsNull(m.LockReason(sing));
    }

    [Test] public void ServerSoldThemesCannotBeBoughtOnThePhone()
    {
        var m = PlayerMeta.Parse("c=999999999");
        int aurora = PlayerMeta.FindTheme("Aurora");
        Assert.IsFalse(m.BuyOrSelect(aurora), "the wallet sells it, not the phone");
        Assert.IsTrue(m.BuyOrSelect(aurora, allowServerSold: true), "preview builds have no backend");
    }

    [Test] public void ServerAnswerOverridesAnEditedSave()
    {
        // someone edited coins and the owned themes bit mask by hand
        long all = (1L << PlayerMeta.Themes.Length) - 1;
        var m = PlayerMeta.Parse($"c=999999999999;o={all};ec=999999999999");
        Assert.IsTrue(m.Owns(PlayerMeta.FindTheme("Singularity")));
        m.ApplyServer(1125, new[] { "Aurora" }, m.EarnedTotal, m.SpentOther);
        Assert.AreEqual(1125, m.Coins);
        Assert.IsFalse(m.Owns(PlayerMeta.FindTheme("Singularity")));
        Assert.IsTrue(m.Owns(PlayerMeta.FindTheme("Aurora")));
        Assert.IsTrue(m.Owns(PlayerMeta.FindTheme("Neon")), "locally sold themes are untouched");
    }

    [Test] public void CoinsEarnedWhileASyncIsInFlightAreKept()
    {
        var m = PlayerMeta.Parse("c=100;ec=100");
        long earnedAtSend = m.EarnedTotal, spentAtSend = m.SpentOther;
        m.AddCoins(30);
        Assert.IsTrue(m.Spend(10));
        m.ApplyServer(500, new string[0], earnedAtSend, spentAtSend);
        Assert.AreEqual(520, m.Coins);
    }

    [Test] public void StoreBundleThemesSurviveAServerAnswer()
    {
        var m = new PlayerMeta();
        Assert.IsTrue(m.Grant(Store.Find("bundle_royal")));
        m.ApplyServer(0, new string[0], m.EarnedTotal, m.SpentOther);
        Assert.IsTrue(m.Owns(PlayerMeta.FindTheme("Royal Gold")));
    }

    [Test] public void CountersAndBigNumbersSurviveSaving()
    {
        var m = PlayerMeta.Parse("c=5000000000;ec=7000000000;sp=2000000000");
        Assert.AreEqual(5000000000L, m.Coins);
        var back = PlayerMeta.Parse(m.Serialize());
        Assert.AreEqual(5000000000L, back.Coins);
        Assert.AreEqual(7000000000L, back.EarnedTotal);
        Assert.AreEqual(2000000000L, back.SpentOther);
        Assert.AreEqual(250L, PlayerMeta.Parse("c=250").EarnedTotal, "an old save claims what it holds");
    }

    [Test] public void MergeNeverLowersTheCounters()
    {
        var a = PlayerMeta.Parse("c=10;ec=900;sp=100;v=3");
        var b = PlayerMeta.Parse("c=20;ec=400;sp=300;v=2");
        var m = PlayerMeta.Merge(a, b);
        Assert.AreEqual(900, m.EarnedTotal);
        Assert.AreEqual(300, m.SpentOther);
    }
}
