namespace BlockDrop.Core
{
    /// <summary>How long to wait before retrying a failed online call. Doubles from 5 seconds up to 5 minutes,
    /// with +-25% jitter so many phones recovering from one outage do not all retry on the same second.
    /// Pure C# so it can be unit tested; <paramref name="jitter01"/> is a random value in [0, 1].</summary>
    public static class RetryBackoff
    {
        public const float BaseSeconds = 5f;
        public const float MaxSeconds = 300f;

        public static float Delay(int failures, float jitter01)
        {
            float d = BaseSeconds;
            for (int i = 0; i < failures && d < MaxSeconds; i++) d *= 2f;
            if (d > MaxSeconds) d = MaxSeconds;
            return d * (0.75f + 0.5f * jitter01);
        }
    }
}
