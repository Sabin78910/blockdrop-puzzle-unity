using System.Linq;
using BlockDrop.Core;
using NUnit.Framework;

public class FairPlayTests
{
    private static Board AlmostFull(int freeX, int freeY)
    {
        var b = new Board(8);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                if (x != freeX || y != freeY) b.SetCell(x, y, 1);
        return b;
    }

    [Test] public void FairDealerAlwaysDealsAPlayableHand()
    {
        var b = AlmostFull(3, 3);
        for (int seed = 1; seed <= 25; seed++)
        {
            var d = new Dealer(seed, p => b.CanPlaceAnywhere(p));
            Assert.IsTrue(d.Hand.Any(p => b.CanPlaceAnywhere(p)), $"seed {seed}");
        }
    }

    [Test] public void ClassicDealerIsUnchanged()
    {
        var a = new Dealer(7);
        var b = new Dealer(7, null);
        CollectionAssert.AreEqual(a.Hand.Select(p => p.Cells), b.Hand.Select(p => p.Cells));
    }

    [Test] public void SecondChanceGivesAPlayableHandOnlyWhenOut()
    {
        var seat = new Seat(11);
        Assert.IsFalse(seat.Revive(), "cannot revive while still playing");
        int guard = 0;
        while (!seat.Out && guard++ < 500)
        {
            bool moved = false;
            for (int i = 0; i < 3 && !moved; i++)
                for (int x = 0; x < 8 && !moved; x++)
                    for (int y = 0; y < 8 && !moved; y++)
                        moved = seat.TryPlace(i, x, y);
            if (!moved) break;
        }
        Assert.IsTrue(seat.Out);
        Assert.IsTrue(seat.Revive());
        Assert.IsFalse(seat.Out);
        Assert.AreEqual(1, seat.Revives);
        Assert.IsTrue(seat.Dealer.Hand.Any(p => p != null && seat.Board.CanPlaceAnywhere(p)));
    }
}

public class AdPolicyTests
{
    private const string Day = "2026-10-10";

    private static AdPolicy AfterGames(int games, string day = Day)
    {
        var a = new AdPolicy();
        for (int i = 0; i < games; i++) a.OnGameEnded(day, false);
        return a;
    }

    [Test] public void NoAdsInTheFirstSessionOrTheFirstGamesOfTheDay()
    {
        Assert.IsFalse(AfterGames(10).ShouldShowInterstitial(Day, 1000, false, firstSession: true));
        Assert.IsFalse(AfterGames(AdPolicy.FreeGamesPerDay).ShouldShowInterstitial(Day, 1000, false, false));
        Assert.IsTrue(AfterGames(AdPolicy.MinGamesBetween).ShouldShowInterstitial(Day, 1000, false, false));
    }

    [Test] public void RespectsGapsCapAndRemoveAds()
    {
        var a = AfterGames(5);
        Assert.IsTrue(a.ShouldShowInterstitial(Day, 1000, false, false));
        a.OnInterstitialShown(1000);
        for (int i = 0; i < AdPolicy.MinGamesBetween; i++) a.OnGameEnded(Day, false);
        Assert.IsFalse(a.ShouldShowInterstitial(Day, 1000 + AdPolicy.MinSecondsBetween - 1, false, false), "too soon");
        Assert.IsTrue(a.ShouldShowInterstitial(Day, 1000 + AdPolicy.MinSecondsBetween, false, false));
        Assert.IsFalse(a.ShouldShowInterstitial(Day, 5000, removeAds: true, firstSession: false));
        for (int i = 0; i < AdPolicy.MaxPerDay; i++) { a.OnInterstitialShown(10000 + i * 1000); for (int g = 0; g < 3; g++) a.OnGameEnded(Day, false); }
        Assert.IsFalse(a.ShouldShowInterstitial(Day, 99999, false, false), "daily cap");
    }

    [Test] public void NeverRightAfterANewBestAndResetsDaily()
    {
        var a = AfterGames(5);
        a.OnGameEnded(Day, newBest: true);
        Assert.IsFalse(a.ShouldShowInterstitial(Day, 1000, false, false));
        a.OnGameEnded("2026-10-11", false);
        Assert.AreEqual(1, a.GamesToday, "new day");
        Assert.IsFalse(a.ShouldShowInterstitial("2026-10-11", 1000, false, false), "first games of the day are free");
    }

    [Test] public void Persists()
    {
        var a = AfterGames(4);
        a.OnInterstitialShown(1234.5);
        var b = AdPolicy.Parse(a.Serialize());
        Assert.AreEqual(a.Day, b.Day);
        Assert.AreEqual(a.GamesToday, b.GamesToday);
        Assert.AreEqual(a.ShownToday, b.ShownToday);
        Assert.AreEqual(1234.5, b.LastAdAt);
    }
}
