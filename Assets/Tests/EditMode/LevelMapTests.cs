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
}
