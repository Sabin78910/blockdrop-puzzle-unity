using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlockDrop.Core;
using Unity.Services.CloudSave;
using UnityEngine;

namespace BlockDrop.Game
{
    /// <summary>Keeps progress (level stars, coins, themes, best score) in Unity Cloud Save so a new phone or a
    /// reinstall can get it back once the player is signed in to the same account. Best effort like the rest of
    /// <see cref="OnlineService"/>: offline or failing simply means progress stays on this phone.
    /// Cost: one read per start and at most one write every <see cref="MinPushSeconds"/>, only when something changed.</summary>
    public static class CloudSync
    {
        private const string Key = "blockdrop_save";
        private const float MinPushSeconds = 600f;

        private static bool _restored;
        private static string _lastCloud = "";
        private static float _lastPushAt = -MinPushSeconds;
        private static bool _hooked, _busy;

        /// <summary>True once, after cloud data changed what is saved on this phone. The game then reloads it.</summary>
        public static bool TakeRestored() { bool r = _restored; _restored = false; return r; }

        private static string Local() => CloudProgress.Pack(
            PlayerPrefs.GetString("progress", ""), PlayerPrefs.GetString("meta", ""), PlayerPrefs.GetInt("best", 0));

        /// <summary>Call after sign-in (and again after switching accounts): merges the cloud copy into this phone, then uploads the result.</summary>
        public static async Task SyncAsync()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var items = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key });
                _lastCloud = items.TryGetValue(Key, out var item) ? item.Value.GetAs<string>() : "";

                string local = Local();
                string merged = CloudProgress.Merge(local, _lastCloud);
                if (merged != local && CloudProgress.TryUnpack(merged, out var progress, out var meta, out int best))
                {
                    PlayerPrefs.SetString("progress", progress);
                    PlayerPrefs.SetString("meta", meta);
                    PlayerPrefs.SetInt("best", best);
                    PlayerPrefs.Save();
                    _restored = true;
                }
                await PushAsync(merged, force: true);
                if (!_hooked) { _hooked = true; Application.focusChanged += OnFocus; _ = LoopAsync(); }
            }
            catch (Exception e) { Debug.LogWarning("Cloud save sync failed: " + e.Message); }
            finally { _busy = false; }
        }

        private static async Task PushAsync(string blob, bool force)
        {
            if (!OnlineService.Ready || blob == _lastCloud) return;
            if (!force && Time.realtimeSinceStartup - _lastPushAt < MinPushSeconds) return;
            await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { [Key] = blob });
            _lastCloud = blob;
            _lastPushAt = Time.realtimeSinceStartup;
        }

        private static async Task PushIfChangedAsync()
        {
            try { await PushAsync(Local(), force: false); }
            catch (Exception e) { Debug.LogWarning("Cloud save upload failed: " + e.Message); }
        }

        private static void OnFocus(bool focused) { if (!focused) _ = PushIfChangedAsync(); }

        private static async Task LoopAsync()
        {
            while (true)
            {
                await Task.Delay((int)(MinPushSeconds * 1000));
                await PushIfChangedAsync();
            }
        }
    }
}
