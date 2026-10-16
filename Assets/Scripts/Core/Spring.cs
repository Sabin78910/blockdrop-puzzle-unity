using System;

namespace BlockDrop.Core
{
    /// <summary>A damped spring for UI and piece motion: overshoot on landing, settle without jitter.
    /// Pure C# so the feel can be unit tested. Frequency is in Hz; damping ratio 1 never overshoots,
    /// below 1 overshoots (0.55 is the landing feel in the design spec).</summary>
    public struct Spring
    {
        public float Value;
        public float Velocity;

        public Spring(float value) { Value = value; Velocity = 0f; }

        /// <summary>Advances toward <paramref name="target"/>. Large frame times are split into small steps,
        /// so a hitch never makes the spring explode.</summary>
        public void Step(float target, float dt, float frequency, float damping)
        {
            if (dt <= 0f) return;
            float omega = 2f * (float)Math.PI * Math.Max(0.01f, frequency);
            float zeta = Math.Max(0f, damping);
            int steps = Math.Max(1, (int)Math.Ceiling(dt / (1f / 240f)));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                float accel = -omega * omega * (Value - target) - 2f * zeta * omega * Velocity;
                Velocity += accel * h;   // semi-implicit Euler: stable for springs at small steps
                Value += Velocity * h;
            }
        }

        public bool IsSettled(float target, float epsilon = 0.001f) =>
            Math.Abs(Value - target) < epsilon && Math.Abs(Velocity) < epsilon * 10f;

        /// <summary>Snaps to the target (used when reduced motion is on).</summary>
        public void Snap(float target) { Value = target; Velocity = 0f; }
    }
}
