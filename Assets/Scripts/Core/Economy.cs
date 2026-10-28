using System;
using System.Globalization;

namespace BlockDrop.Core
{
    /// <summary>Late-game coin economy. Coins only buy cosmetics, so the ladder up to one billion is made reachable
    /// by Fortune instead of by a second currency: coin rewards are multiplied by (price of the best theme owned) / 9,000.
    /// A daily player who owns a Legendary theme reaches the first Mythic theme in about three weeks and the billion tier in about fourteen months (docs/ECONOMY.md).
    /// CloudCode/Wallet.js mirrors <see cref="Fortune"/> and <see cref="ServerMinPrice"/>; WalletTests.js keeps them in step.</summary>
    public static class Economy
    {
        public const long ServerMinPrice = 5000, FortuneBase = 9000;

        public static double Fortune(long bestThemePrice) => Math.Max(1.0, bestThemePrice / (double)FortuneBase);

        /// <summary>Short form that fits a coin pill: 9,999 and below in full, then 12.3K, 1.2M, 1.25B.</summary>
        public static string Compact(long n)
        {
            if (n < 0) n = 0;
            if (n < 10000) return n.ToString("N0", CultureInfo.InvariantCulture);
            string[] suffix = { "K", "M", "B", "T" };
            decimal v = n; // decimal keeps 1,150,000 at exactly 1.15M; a double would truncate it to 1.14M
            int i = -1;
            while (v >= 1000 && i < suffix.Length - 1) { v /= 1000; i++; }
            decimal scale = v >= 100 ? 1m : v >= 10 ? 10m : 100m; // three significant digits, truncated so 999,999 never reads 1000K
            return (Math.Truncate(v * scale) / scale).ToString("0.##", CultureInfo.InvariantCulture) + suffix[i];
        }

        /// <summary>Whole days until <paramref name="price"/> at <paramref name="perDay"/> coins a day (at least 1).</summary>
        public static long DaysToAfford(long shortBy, long perDay) => shortBy <= 0 ? 0 : Math.Max(1, (shortBy + Math.Max(1, perDay) - 1) / Math.Max(1, perDay));
    }
}
