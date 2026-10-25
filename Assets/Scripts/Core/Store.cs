using System;

namespace BlockDrop.Core
{
    public sealed class Product
    {
        public string Id, Title, Price, Badge = "";
        public int Coins;
        public string[] Themes = Array.Empty<string>();
        public bool RemoveAds, Consumable;
    }

    /// <summary>What real money buys. Every product says exactly what is inside: no loot boxes, no random rewards,
    /// no timers or "only today" prices (docs/FEEL.md). Prices are the Play Console defaults in USD; the store
    /// shows the local price from Google Play when it is connected. Product ids must match the Play Console.</summary>
    public static class Store
    {
        public static readonly Product[] Products =
        {
            new Product { Id = "coins_1200",  Title = "Pouch of coins",   Price = "$1.99",  Coins = 1200,  Consumable = true },
            new Product { Id = "coins_3500",  Title = "Chest of coins",   Price = "$4.99",  Coins = 3500,  Consumable = true, Badge = "POPULAR" },
            new Product { Id = "coins_8000",  Title = "Vault of coins",   Price = "$9.99",  Coins = 8000,  Consumable = true },
            new Product { Id = "coins_20000", Title = "Treasury of coins", Price = "$19.99", Coins = 20000, Consumable = true, Badge = "BEST VALUE" },
            new Product { Id = "remove_ads",  Title = "Remove ads",       Price = "$2.99",  RemoveAds = true },
            new Product { Id = "bundle_royal", Title = "Royal bundle",    Price = "$7.99",  Coins = 2000, Themes = new[] { "Royal Gold" }, RemoveAds = true },
            new Product { Id = "bundle_legends", Title = "Legends collection", Price = "$24.99", Coins = 10000, Themes = new[] { "Aurora", "Galaxy", "Diamond", "Royal Gold" }, RemoveAds = true, Badge = "ALL LEGENDARY" },
        };

        public static Product Find(string id) => Array.Find(Products, p => p.Id == id);

        /// <summary>One line listing exactly what the product contains.</summary>
        public static string Contents(Product p)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (p.Coins > 0) parts.Add(p.Coins.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " ●");
            foreach (var t in p.Themes) parts.Add(Loc.F("{0} theme", Loc.L(t)));
            if (p.RemoveAds) parts.Add(Loc.L("no ads"));
            return string.Join(" + ", parts);
        }
    }
}
