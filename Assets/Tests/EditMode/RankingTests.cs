using BlockDrop.Core;
using NUnit.Framework;

public class RankingTests
{
    [TestCase("Tester_1#79410", "Tester_1", "#79410")]
    [TestCase("Swift#1", "Swift", "#1")]
    [TestCase("NoTag", "NoTag", "")]
    [TestCase("", "Player", "")]
    public void SplitsNameAndTag(string full, string name, string tag)
    {
        Ranking.SplitName(full, out var n, out var t);
        Assert.AreEqual(name, n); Assert.AreEqual(tag, t);
    }

    [Test] public void EmblemIsStableAndInRange()
    {
        Assert.AreEqual(Ranking.Emblem("Tester_1#79410"), Ranking.Emblem("Tester_1#79410"));
        for (int i = 0; i < 200; i++) Assert.That(Ranking.Emblem("P" + i + "#" + i), Is.InRange(0, Ranking.EmblemCount - 1));
    }

    [Test] public void EmblemsSpreadAcrossPlayers()
    {
        var seen = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < 60; i++) seen.Add(Ranking.Emblem("Player" + i + "#" + (1000 + i)));
        Assert.AreEqual(Ranking.EmblemCount, seen.Count);
    }

    [TestCase("tester#1", "T")] [TestCase("_a#1", "A")] [TestCase("", "P")] [TestCase("___#1", "?")]
    public void InitialSkipsPunctuation(string name, string expected) => Assert.AreEqual(expected, Ranking.Initial(name));

    [TestCase(1, 1)] [TestCase(3, 3)] [TestCase(4, 0)] [TestCase(0, 0)]
    public void MedalsOnlyForTopThree(int rank, int medal) => Assert.AreEqual(medal, Ranking.Medal(rank));

    [Test] public void ScoreHasThousandsSeparators() => Assert.AreEqual("501,474", Ranking.FormatScore(501474));
}
