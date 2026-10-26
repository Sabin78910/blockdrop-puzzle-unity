using System;
using BlockDrop.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace BlockDrop.Game
{
    /// <summary>Unity Gaming Services: anonymous sign-in + global leaderboards.
    /// Every call is best-effort — offline or misconfigured simply means "no leaderboard",
    /// the game itself never depends on it.</summary>
    public static class OnlineService
    {
        // Leaderboard IDs must match the Unity Cloud dashboard exactly (case-sensitive).
        public const string ClassicBoard = "Classic";
        public const string DailyBoard = "Daily";
        public const string LevelsBoard = "Levels"; // total level stars

        public struct Row { public int Rank; public string Name; public long Score; public bool IsMe; }

        public static bool Ready { get; private set; }
        /// <summary>Failed sign-in attempts since the last success (offline phones do not count). Drives the retry backoff.</summary>
        public static int Failures { get; private set; }
        public static string Status { get; private set; } = "Connecting…";
        public static string PlayerName { get; private set; } = "";
        public static readonly Dictionary<string, List<Row>> Top = new Dictionary<string, List<Row>>();
        public static readonly Dictionary<string, string> Errors = new Dictionary<string, string>();

        private static Task _init;

        /// <summary>Starts (or retries) sign-in. A finished attempt that left us offline is retried,
        /// so the game comes online by itself when the network returns.</summary>
        public static Task EnsureReady()
        {
            if (_init == null || (_init.IsCompleted && !Ready)) _init = InitAsync();
            return _init;
        }

        private static async Task InitAsync()
        {
            try
            {
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    Status = "Offline — playing without leaderboards";
                    return; // EnsureReady retries later

                }
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                PlayerName = await AuthenticationService.Instance.GetPlayerNameAsync(); // auto-generated, e.g. "SwiftFox#1234"
                Ready = true;
                Failures = 0;
                Status = "Online";
                _ = CloudSync.SyncAsync();
            }
            catch (Exception e)
            {
                Failures++;
                Status = "Offline — playing without leaderboards";
                Debug.LogWarning("Online services unavailable: " + e.Message);
            }
        }

        /// <summary>Call after the signed-in player changes (Continue with Google switching to an earlier install's player).</summary>
        public static async Task AfterAccountChangeAsync()
        {
            PlayerName = await AuthenticationService.Instance.GetPlayerNameAsync();
            Top.Clear();
        }

        /// <summary>Submits through the SubmitScore Cloud Code script (CloudCode/SubmitScore.js), which checks
        /// the score against the placements and lines before writing it; players cannot write leaderboards directly.</summary>
        public static async void SubmitScore(string board, Board result)
        {
            try
            {
                if (IsPreview) return; // test builds never post to the live leaderboard
                await EnsureReady();
                if (!Ready || result.Score <= 0) return;
                var args = new Dictionary<string, object>
                {
                    ["leaderboardId"] = board, ["score"] = result.Score,
                    ["placements"] = result.Placements, ["lines"] = result.LinesCleared,
                };
                var reply = await CloudCodeService.Instance.CallEndpointAsync<SubmitReply>("SubmitScore", args);
                if (!reply.accepted) Debug.LogWarning($"Score rejected ({board}): {reply.reason}");
                await RefreshAsync(board);
            }
            catch (Exception e) { Debug.LogWarning($"Score submit failed ({board}): {e.Message}"); }
        }

        /// <summary>The Preview Cloud Build target installs as com.sabin.blockdrop.preview.</summary>
        public static bool IsPreview => Application.identifier.EndsWith("preview", StringComparison.Ordinal);

        private static int _levelsSent;

        /// <summary>Posts the player's total level stars to the Levels board when they have grown (never from preview
        /// builds), then reloads that board. Called after a level win and when Rankings opens, so stars earned
        /// before the board existed still appear.</summary>
        public static async void SubmitLevels(int totalStars)
        {
            try
            {
                await EnsureReady();
                if (!Ready) return;
                if (!IsPreview && totalStars > _levelsSent)
                {
                    var args = new Dictionary<string, object> { ["leaderboardId"] = LevelsBoard, ["score"] = totalStars, ["placements"] = 0, ["lines"] = 0 };
                    var reply = await CloudCodeService.Instance.CallEndpointAsync<SubmitReply>("SubmitScore", args);
                    if (reply.accepted) _levelsSent = totalStars; else Debug.LogWarning($"Stars rejected: {reply.reason}");
                }
                await RefreshAsync(LevelsBoard);
            }
            catch (Exception e) { Debug.LogWarning($"Stars submit failed: {e.Message}"); }
        }

        [Serializable] private class SubmitReply { public bool accepted; public string reason; }

        public static async void Refresh(string board)
        {
            Errors.Remove(board);
            try { await RefreshAsync(board); }
            catch (Exception e)
            {
                Errors[board] = "Leaderboard unavailable right now.\nPlease try again later.";
                Debug.LogWarning($"Leaderboard '{board}' failed: {e.Message}");
            }
        }

        /// <summary>Sets the public player name (letters, digits, underscore; 3–20 chars). UGS adds a #1234 suffix.</summary>
        public static async System.Threading.Tasks.Task<string> SetNameAsync(string name)
        {
            try
            {
                await EnsureReady();
                if (!Ready) return "You're offline. Your name will be saved locally.";
                PlayerName = await AuthenticationService.Instance.UpdatePlayerNameAsync(name);
                return null;
            }
            catch (Exception e) { Debug.LogWarning("Name update failed: " + e.Message); return "Couldn't save the name online. Try another name."; }
        }

        private static async Task RefreshAsync(string board)
        {
            await EnsureReady();
            if (!Ready) { Errors[board] = Status; return; }
            var page = await LeaderboardsService.Instance.GetScoresAsync(board, new GetScoresOptions { Limit = 10 });
            var rows = new List<Row>();
            foreach (var e in page.Results)
                rows.Add(new Row { Rank = e.Rank + 1, Name = NameFilter.ForDisplay(e.PlayerName), Score = (long)e.Score, IsMe = e.PlayerId == AuthenticationService.Instance.PlayerId });
            Top[board] = rows;
        }
    }
}
