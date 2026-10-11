using BlockDrop.Core;
using NUnit.Framework;

public class NameFilterTests
{
    [TestCase("SwiftFox")]
    [TestCase("Sabin_77")]
    [TestCase("Classic_Pro")]
    [TestCase("Cassandra")]
    [TestCase("Dickens_Fan")]
    [TestCase("Computador")]
    public void OrdinaryNamesPass(string name) => Assert.IsTrue(NameFilter.IsAllowed(name), name);

    [TestCase("fuck")]
    [TestCase("FuckYou")]
    [TestCase("f_u_c_k")]
    [TestCase("sh1t_lord")]
    [TestCase("B1TCH")]
    [TestCase("mierda99")]
    [TestCase("caralho")]
    [TestCase("muji_ko")]
    [TestCase("hitler_88")]
    public void OffensiveNamesAreBlocked(string name) => Assert.IsFalse(NameFilter.IsAllowed(name), name);

    [Test] public void ShortWordsAreBlockedOnlyAsWholeTokens()
    {
        Assert.IsFalse(NameFilter.IsAllowed("ass"));
        Assert.IsFalse(NameFilter.IsAllowed("big_ass"));
        Assert.IsTrue(NameFilter.IsAllowed("Assassin"));
        Assert.IsTrue(NameFilter.IsAllowed("Classic"));
        Assert.IsFalse(NameFilter.IsAllowed("puta"));
        Assert.IsTrue(NameFilter.IsAllowed("Computador"));
    }

    [Test] public void SquashUndoesLeetAndSeparators()
    {
        Assert.AreEqual("shit", NameFilter.Squash("Sh_1t"));
        Assert.AreEqual("seal", NameFilter.Squash("5-3-@-L"));
        Assert.AreEqual("", NameFilter.Squash(null));
    }

    [Test] public void LeaderboardHidesBadNamesButKeepsTheTag()
    {
        Assert.AreEqual("SwiftFox#1234", NameFilter.ForDisplay("SwiftFox#1234"));
        Assert.AreEqual("Player#4321", NameFilter.ForDisplay("shit_head#4321"));
        Assert.AreEqual("Player", NameFilter.ForDisplay("fuck"));
        Assert.AreEqual("Player", NameFilter.ForDisplay(""));
    }
}
