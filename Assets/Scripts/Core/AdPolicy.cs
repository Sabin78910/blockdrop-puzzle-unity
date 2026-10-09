using System;
using System.Globalization;

namespace BlockDrop.Core
{
    /// <summary>Player-friendly ad pacing, the opposite of the #1 complaint about competitors ("an ad after every game"):
    /// no ads in the first session, the first games of each day are ad-free, never right after a new best,
    /// a minimum gap in games and seconds, and a daily cap. Pure logic so it is fully testable.</summary>
    public sealed class AdPolicy
    {
        public const int FreeGamesPerDay = 2, MinGamesBetween = 3, MinSecondsBetween = 120, MaxPerDay = 6;

        public string Day { get; private set; } = "";
        public int GamesToday { get; private set; }
        public int GamesSinceAd { get; private set; }
        public int ShownToday { get; private set; }
        public double LastAdAt { get; private set; } = -1e9;
        public bool LastWasNewBest { get; private set; }

        private void RollDay(string today)
        {
            if (Day == today) return;
            Day = today;
            GamesToday = 0;
            ShownToday = 0;
        }

        public void OnGameEnded(string today, bool newBest)
        {
            RollDay(today);
            GamesToday++;
            GamesSinceAd++;
            LastWasNewBest = newBest;
        }

        /// <param name="now">Seconds on any monotonic clock that survives restarts (for example Unix time).</param>
        public bool ShouldShowInterstitial(string today, double now, bool removeAds, bool firstSession)
        {
            RollDay(today);
            if (removeAds || firstSession || LastWasNewBest) return false;
            if (GamesToday <= FreeGamesPerDay || GamesSinceAd < MinGamesBetween) return false;
            if (now - LastAdAt < MinSecondsBetween || ShownToday >= MaxPerDay) return false;
            return true;
        }

        public void OnInterstitialShown(double now)
        {
            GamesSinceAd = 0;
            LastAdAt = now;
            ShownToday++;
        }

        public string Serialize() => string.Join(";",
            "d=" + Day, "g=" + GamesToday, "s=" + GamesSinceAd, "n=" + ShownToday,
            "l=" + LastAdAt.ToString("R", CultureInfo.InvariantCulture), "b=" + (LastWasNewBest ? 1 : 0));

        public static AdPolicy Parse(string data)
        {
            var a = new AdPolicy();
            if (string.IsNullOrEmpty(data)) return a;
            foreach (var part in data.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                int.TryParse(v, out int n);
                switch (k)
                {
                    case "d": a.Day = v; break;
                    case "g": a.GamesToday = Math.Max(0, n); break;
                    case "s": a.GamesSinceAd = Math.Max(0, n); break;
                    case "n": a.ShownToday = Math.Max(0, n); break;
                    case "l": if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double t)) a.LastAdAt = t; break;
                    case "b": a.LastWasNewBest = n == 1; break;
                }
            }
            return a;
        }
    }
}
