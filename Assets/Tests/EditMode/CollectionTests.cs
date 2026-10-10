using System.Linq;
using BlockDrop.Core;
using NUnit.Framework;

public class CollectionTests
{
    [Test] public void OnePictureFor10LevelsAndValidArt()
    {
        Assert.AreEqual(LevelLibrary.Count, Collection.Pictures.Length * Collection.TilesPerPicture);
        foreach (var pic in Collection.Pictures)
        {
            Assert.AreEqual(Collection.Height, pic.Rows.Length, pic.Name);
            Assert.IsTrue(pic.Rows.All(r => r.Length == Collection.Width), pic.Name + " row width");
            Assert.IsTrue(pic.Rows.SelectMany(r => r).All(c => pic.Colors.ContainsKey(c)), pic.Name + " palette");
        }
        Assert.AreEqual(Collection.TilesPerPicture, Enumerable.Range(0, Collection.Width * Collection.Height)
            .Select(i => Collection.TileAt(i % Collection.Width, i / Collection.Width)).Distinct().Count());
    }

    [Test] public void BeatingLevelsRevealsPiecesAndCompletesPictures()
    {
        var p = new Progress();
        Assert.AreEqual((0, 0), Collection.ForLevel(1));
        Assert.AreEqual((1, 2), Collection.ForLevel(13));
        for (int n = 1; n <= 9; n++) p.Record(n, 1);
        Assert.AreEqual(9, Collection.RevealedCount(p, 0));
        Assert.IsFalse(Collection.Complete(p, 0));
        p.Record(10, 3);
        Assert.IsTrue(Collection.Complete(p, 0));
        Assert.AreEqual(1, Collection.CompletedPictures(p));
        Assert.AreEqual(0, Collection.RevealedCount(p, 1));
    }
}
