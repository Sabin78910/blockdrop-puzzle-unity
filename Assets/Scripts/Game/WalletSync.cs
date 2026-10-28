using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlockDrop.Core;
using Unity.Services.CloudCode;
using UnityEngine;

namespace BlockDrop.Game
{
    /// <summary>Talks to the Wallet Cloud Code script (CloudCode/Wallet.js). The server holds the coin balance and the
    /// themes sold above <see cref="Economy.ServerMinPrice"/>; the phone reports its lifetime counters and takes the
    /// answer. One request at a time, best effort: offline just means the phone keeps its own numbers until the next sync.</summary>
    public static class WalletSync
    {
        [Serializable] private class Reply { public bool ok; public string reason; public long balance; public string[] owned; public double fortune; }

        public enum Outcome { Synced, Bought, Refused, Offline }

        public struct Result { public Outcome Outcome; public string Reason; }

        private static Task<Result> _running;

        /// <summary>True while a request is in flight.</summary>
        public static bool Busy => _running != null && !_running.IsCompleted;

        /// <summary>Whether this build and this moment can reach the wallet at all.</summary>
        public static bool Available => OnlineService.Ready && !OnlineService.IsPreview && !_missing;

        // The Wallet script is not published in this project yet: stop asking for the rest of the session.
        private static bool _missing;
        public static bool Missing => _missing;

        public static Task<Result> SyncAsync(PlayerMeta meta) => Run(meta, "sync", "");
        public static Task<Result> BuyAsync(PlayerMeta meta, string theme) => Run(meta, "buy", theme);

        private static Task<Result> Run(PlayerMeta meta, string op, string theme)
        {
            if (Busy) return _running;
            return _running = RunAsync(meta, op, theme);
        }

        private static async Task<Result> RunAsync(PlayerMeta meta, string op, string theme)
        {
            if (!Available) return new Result { Outcome = Outcome.Offline, Reason = "Go online to buy" };
            try
            {
                long earned = meta.EarnedTotal, spent = meta.SpentOther;
                var args = new Dictionary<string, object>
                {
                    ["op"] = op, ["earned"] = earned, ["spent"] = spent, ["theme"] = theme,
                    ["legacyOwned"] = meta.ClaimedServerThemes(),
                };
                var reply = await CloudCodeService.Instance.CallEndpointAsync<Reply>("Wallet", args);
                meta.ApplyServer(reply.balance, reply.owned ?? new string[0], earned, spent);
                if (op == "buy") return reply.ok ? new Result { Outcome = Outcome.Bought } : new Result { Outcome = Outcome.Refused, Reason = reply.reason };
                return new Result { Outcome = Outcome.Synced };
            }
            catch (Exception e)
            {
                string m = e.Message ?? "";
                if (m.Contains("404") || m.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 || m.IndexOf("notfound", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _missing = true;
                    Debug.LogWarning("Wallet service is not published; expensive themes stay unavailable this session: " + m);
                    return new Result { Outcome = Outcome.Offline, Reason = "" };
                }
                Debug.LogWarning("Wallet request failed: " + m);
                return new Result { Outcome = Outcome.Offline, Reason = "Purchase failed, try again" };
            }
        }
    }
}
