using BlockDrop.Core;
using NUnit.Framework;

public class SpringTests
{
    private static Spring Run(float damping, float seconds, out float peak)
    {
        var s = new Spring(0f);
        peak = 0f;
        for (float t = 0; t < seconds; t += 1f / 60f) { s.Step(1f, 1f / 60f, 4f, damping); peak = System.Math.Max(peak, s.Value); }
        return s;
    }

    [Test] public void UnderdampedOvershootsThenSettles()
    {
        var s = Run(0.55f, 3f, out float peak);
        Assert.Greater(peak, 1.05f, "landing should overshoot");
        Assert.Less(peak, 1.4f, "but not wildly");
        Assert.IsTrue(s.IsSettled(1f));
    }

    [Test] public void CriticallyDampedNeverOvershoots()
    {
        Run(1f, 3f, out float peak);
        Assert.LessOrEqual(peak, 1.0005f);
    }

    [Test] public void LongFrameDoesNotExplode()
    {
        var s = new Spring(0f);
        s.Step(1f, 0.5f, 6f, 0.55f);   // a half-second hitch
        Assert.Less(System.Math.Abs(s.Value), 2f);
    }

    [Test] public void ZeroTimeAndSnapAreStable()
    {
        var s = new Spring(3f);
        s.Step(1f, 0f, 4f, 0.5f);
        Assert.AreEqual(3f, s.Value);
        s.Snap(1f);
        Assert.IsTrue(s.IsSettled(1f));
    }
}
