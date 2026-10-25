using BlockDrop.Core;
using NUnit.Framework;

public class CloudProgressTests
{
    private static string Blob(string progress, string meta, int best) => CloudProgress.Pack(progress, meta, best);

    [Test] public void PackAndUnpackRoundTrip()
    {
        var blob = Blob("123", "c=5;s=1", 900);
        Assert.IsTrue(CloudProgress.TryUnpack(blob, out var p, out var m, out int best));
        Assert.AreEqual("123", p);
        Assert.AreEqual("c=5;s=1", m);
        Assert.AreEqual(900, best);
    }

    [Test] public void RejectsGarbage()
    {
        Assert.IsFalse(CloudProgress.TryUnpack("", out _, out _, out _));
        Assert.IsFalse(CloudProgress.TryUnpack("2|1|x|y", out _, out _, out _));
        Assert.IsFalse(CloudProgress.TryUnpack("1|abc|x|y", out _, out _, out _));
    }

    [Test] public void NewPhoneTakesEverythingFromTheCloud()
    {
        var cloudMeta = new PlayerMeta();
        var cloud = Blob("3321", cloudMeta.Serialize(), 4200);
        var fresh = Blob("", new PlayerMeta().Serialize(), 0);
        CloudProgress.TryUnpack(CloudProgress.Merge(fresh, cloud), out var p, out _, out int best);
        Assert.AreEqual(3, Progress.Parse(p).StarsFor(1));
        Assert.AreEqual(1, Progress.Parse(p).StarsFor(4));
        Assert.AreEqual(4200, best);
    }

    [Test] public void LevelStarsKeepTheBestOfBoth()
    {
        var a = Blob("31", new PlayerMeta().Serialize(), 100);
        var b = Blob("23", new PlayerMeta().Serialize(), 50);
        CloudProgress.TryUnpack(CloudProgress.Merge(a, b), out var p, out _, out int best);
        var progress = Progress.Parse(p);
        Assert.AreEqual(3, progress.StarsFor(1));
        Assert.AreEqual(3, progress.StarsFor(2));
        Assert.AreEqual(100, best);
    }

    [Test] public void MergeIsTheSameBothWays()
    {
        var a = Blob("31", new PlayerMeta().Serialize(), 100);
        var b = Blob("23", new PlayerMeta().Serialize(), 50);
        Assert.AreEqual(CloudProgress.Merge(a, b), CloudProgress.Merge(b, a));
    }

    [Test] public void UnreadableCloudCopyLeavesThisPhoneUnchanged()
    {
        var local = Blob("31", new PlayerMeta().Serialize(), 100);
        Assert.AreEqual(local, CloudProgress.Merge(local, "not a save"));
        Assert.AreEqual(local, CloudProgress.Merge("", local));
    }
}
