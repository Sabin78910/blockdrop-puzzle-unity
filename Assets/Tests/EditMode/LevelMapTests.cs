using BlockDrop.Core;
using NUnit.Framework;

public class LevelMapTests
{
    [Test] public void NodesStayOnScreenAndWindLeftAndRight()
    {
        float min = 1, max = 0;
        for (int n = 1; n <= 200; n++)
        {
            float x = LevelMap.NodeX(n);
            Assert.That(x, Is.InRange(0.12f, 0.88f), "level " + n);
            min = System.Math.Min(min, x); max = System.Math.Max(max, x);
        }
        Assert.Less(min, 0.25f);
        Assert.Greater(max, 0.75f);
    }

    [Test] public void ChaptersStartEveryTenLevels()
    {
        Assert.IsTrue(LevelMap.StartsChapter(1));
        Assert.IsFalse(LevelMap.StartsChapter(10));
        Assert.IsTrue(LevelMap.StartsChapter(11));
        Assert.AreEqual(1, LevelMap.ChapterOf(10));
        Assert.AreEqual(2, LevelMap.ChapterOf(11));
        Assert.AreEqual(100000, LevelMap.ChapterOf(LevelLibrary.Count));
    }

    [Test] public void VisibleRangeIsSmallAndStaysInBounds()
    {
        var (f, l) = LevelMap.Visible(0.5f, 6.3f);
        Assert.AreEqual(1, f);
        Assert.LessOrEqual(l - f, 10);
        (f, l) = LevelMap.Visible(LevelMap.ClampBottom(5e9f, 6.3f), 6.3f);
        Assert.LessOrEqual(l, LevelLibrary.Count);
        Assert.Greater(l, f);
    }

    [Test] public void MapOpensNearTheNextLevelAndNeverScrollsPastTheEnds()
    {
        Assert.AreEqual(0.5f, LevelMap.StartBottom(1));
        Assert.AreEqual(47f, LevelMap.StartBottom(50));
        Assert.AreEqual(0.5f, LevelMap.ClampBottom(-20, 6.3f));
        Assert.LessOrEqual(LevelMap.ClampBottom(2e6f, 6.3f), LevelLibrary.Count);
    }

    [Test] public void EveryChapterHasATheme()
    {
        Assert.AreEqual(LevelMap.ThemeNames.Length, LevelMap.ThemeColors.Length);
        Assert.AreEqual(0, LevelMap.ThemeOf(1));
        Assert.AreEqual(1, LevelMap.ThemeOf(2));
        Assert.AreEqual(0, LevelMap.ThemeOf(1 + LevelMap.ThemeNames.Length));
        foreach (var n in LevelMap.ThemeNames) Assert.IsTrue(Loc.Table.ContainsKey(n), n);
        var (a, b, t) = LevelMap.Blend(1f);
        Assert.AreEqual((0, 1, 0f), (a, b, t));
        Assert.AreEqual(1, LevelMap.Blend(11f).from);
        Assert.That(LevelMap.Blend(5f).t, Is.InRange(0.1f, 0.9f));
    }

    [Test] public void ScenerySitsOppositeTheNodeAndIsStable()
    {
        int shown = 0;
        for (int n = 1; n <= 300; n++)
        {
            var (kind, x) = LevelMap.PropFor(n);
            Assert.AreEqual((kind, x), LevelMap.PropFor(n));
            if (kind < 0) continue;
            shown++;
            Assert.That(kind, Is.InRange(0, 2));
            Assert.That(x, Is.InRange(0.05f, 0.95f));
            Assert.Greater(System.Math.Abs(x - LevelMap.NodeX(n)), 0.25f, "level " + n);
        }
        Assert.That(shown, Is.InRange(100, 250));
    }

    [Test] public void EveryThemeHasItsArtOnDisk()
    {
        Assert.AreEqual(LevelMap.ThemeNames.Length, LevelMap.ThemeKeys.Length);
        foreach (var k in LevelMap.ThemeKeys) Assert.IsTrue(System.IO.File.Exists("Assets/Resources/Map/bg_" + k + ".png"), k);
        foreach (var f in new[] { "node", "ribbon", "tree", "house", "lamp", "star_on", "star_off", "crown", "lock" })
            Assert.IsTrue(System.IO.File.Exists("Assets/Resources/Map/" + f + ".png"), f);
    }
}
