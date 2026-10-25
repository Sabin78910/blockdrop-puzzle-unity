using BlockDrop.Core;
using NUnit.Framework;

public class RetryBackoffTests
{
    [Test] public void FirstRetryWaitsAboutFiveSeconds()
    {
        Assert.AreEqual(5f, RetryBackoff.Delay(0, 0.5f), 0.001f);
    }

    [Test] public void DoublesAfterEachFailure()
    {
        Assert.AreEqual(10f, RetryBackoff.Delay(1, 0.5f), 0.001f);
        Assert.AreEqual(20f, RetryBackoff.Delay(2, 0.5f), 0.001f);
    }

    [Test] public void StopsGrowingAtFiveMinutes()
    {
        Assert.AreEqual(300f, RetryBackoff.Delay(6, 0.5f), 0.001f);
        Assert.AreEqual(300f, RetryBackoff.Delay(1000, 0.5f), 0.001f);
    }

    [Test] public void JitterStaysWithinAQuarter()
    {
        Assert.AreEqual(7.5f, RetryBackoff.Delay(1, 0f), 0.001f);
        Assert.AreEqual(12.5f, RetryBackoff.Delay(1, 1f), 0.001f);
    }
}
