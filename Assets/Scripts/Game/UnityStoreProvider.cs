using System;
using System.Collections.Generic;
using BlockDrop.Core;
using UnityEngine;
using UnityEngine.Purchasing;

namespace BlockDrop.Game
{
    /// <summary>Google Play Billing through Unity IAP 5. Product ids come from <see cref="Store.Products"/> and must
    /// exist in the Play Console. <see cref="Ready"/> turns true only once the store has returned the products.</summary>
    public sealed class UnityStoreProvider : IStoreProvider
    {
        private readonly StoreController _store = UnityIAPServices.StoreController();
        private Action<bool> _buyDone;
        private Action<string> _restored;

        public bool Ready { get; private set; }

        public UnityStoreProvider() => Init();

        private async void Init()
        {
            _store.OnProductsFetched += _ => Ready = true;
            _store.OnProductsFetchFailed += f => Debug.LogWarning("IAP products failed: " + f.FailureReason);
            _store.OnPurchasePending += order =>
            {
                string id = order.CartOrdered.Items()[0].Product.definition.id;
                // a purchase that finished after a restart has no buy callback; deliver it as a restore
                if (_buyDone != null) { var done = _buyDone; _buyDone = null; done(true); } else _restored?.Invoke(id);
                _store.ConfirmPurchase(order);
            };
            _store.OnPurchaseFailed += f => { var done = _buyDone; _buyDone = null; done?.Invoke(false); };
            _store.OnPurchaseDeferred += _ => { var done = _buyDone; _buyDone = null; done?.Invoke(false); };
            _store.OnPurchasesFetched += orders =>
            {
                foreach (var o in orders.ConfirmedOrders)
                {
                    var p = Store.Find(o.CartOrdered.Items()[0].Product.definition.id);
                    if (p != null && !p.Consumable) _restored?.Invoke(p.Id);
                }
            };
            try
            {
                await _store.Connect();
                var defs = new List<ProductDefinition>();
                foreach (var p in Store.Products) defs.Add(new ProductDefinition(p.Id, p.Consumable ? ProductType.Consumable : ProductType.NonConsumable));
                _store.FetchProducts(defs);
            }
            catch (Exception e) { Debug.LogWarning("IAP connect failed: " + e.Message); }
        }

        public string LocalPrice(string productId) => _store.GetProductById(productId)?.metadata.localizedPriceString;

        public void Buy(string productId, Action<bool> onDone)
        {
            var product = _store.GetProductById(productId);
            if (!Ready || product == null || _buyDone != null) { onDone?.Invoke(false); return; }
            _buyDone = onDone;
            _store.PurchaseProduct(product);
        }

        public void Restore(Action<string> onRestored)
        {
            _restored = onRestored;
            _store.FetchPurchases();
        }
    }
}
