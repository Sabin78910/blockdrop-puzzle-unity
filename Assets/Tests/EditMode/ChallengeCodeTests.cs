using System.Linq;
using BlockDrop.Core;
using NUnit.Framework;

public class ChallengeCodeTests
{
    [Test] public void RoundTripsSeeds()
    {
        var rng = new System.Random(1);
        for (int i = 0; i < 500; i++)
        {
            int seed = ChallengeCode.NewSeed(rng);
            string code = ChallengeCode.Encode(seed);
            Assert.AreEqual(ChallengeCode.Length, code.Length);
            Assert.IsTrue(ChallengeCode.TryDecode(code, out int back));
            Assert.AreEqual(seed, back);
        }
    }

    [Test] public void AcceptsLowercaseAndSpaces()
    {
        string code = ChallengeCode.Encode(123456);
        Assert.IsTrue(ChallengeCode.TryDecode("  " + code.ToLowerInvariant() + " ", out int seed));
        Assert.AreEqual(123456, seed);
    }

    [Test] public void RejectsBadCodes()
    {
        Assert.IsFalse(ChallengeCode.TryDecode(null, out _));
        Assert.IsFalse(ChallengeCode.TryDecode("ABC", out _));
        Assert.IsFalse(ChallengeCode.TryDecode("ABCDE!", out _));
    }

    [Test] public void SameCodeGivesSamePieces()
    {
        ChallengeCode.TryDecode(ChallengeCode.Encode(777), out int seed);
        var a = new Dealer(seed);
        var b = new Dealer(777);
        CollectionAssert.AreEqual(a.Hand.Select(p => p.Cells), b.Hand.Select(p => p.Cells));
    }
}
