using System;

namespace BlockDrop.Game
{
    /// <summary>The game talks to the app store only through this interface, so Unity IAP (Google Play Billing)
    /// can be plugged in without touching gameplay code, the same way ads go through <see cref="IAdProvider"/>.</summary>
    public interface IStoreProvider
    {
        /// <summary>False until a real store is connected; the shop then shows prices but cannot sell.</summary>
        bool Ready { get; }
        /// <summary>The localized price from the store, or null to use the catalog price.</summary>
        string LocalPrice(string productId);
        /// <summary>Starts a purchase; <paramref name="onDone"/> gets true only once the store confirms payment.</summary>
        void Buy(string productId, Action<bool> onDone);
        /// <summary>Re-delivers non-consumables bought on another install.</summary>
        void Restore(Action<string> onRestored);
    }

    /// <summary>Until the Play Console products exist: nothing can be bought and nothing is given away.</summary>
    public sealed class BetaStoreProvider : IStoreProvider
    {
        public bool Ready => false;
        public string LocalPrice(string productId) => null;
        public void Buy(string productId, Action<bool> onDone) => onDone?.Invoke(false);
        public void Restore(Action<string> onRestored) { }
    }
}
