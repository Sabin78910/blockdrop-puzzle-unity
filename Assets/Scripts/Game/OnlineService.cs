using System;
using BlockDrop.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Exceptions;
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
        public const string KingBoard = "King"; // server-owned rank points (CloudCode/SUBMITRANK.md)
        public const int KingTop = 100;

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

        /// <summary>The player's place on the King board (1 = best), 0 when unranked or unknown.</summary>
        public static int KingRank { get; private set; }

        /// <summary>King needs a top-100 place on the server board AND a linked Google account: the server cannot
        /// see linking, and anonymous ids are free to farm.</summary>
        public static bool IsKing => KingRank > 0 && KingRank <= KingTop && AccountLink.Linked;

        /// <summary>Sends a finished Classic or Daily game to SubmitRank, which keeps the points that decide King.
        /// The phone's own rank bar never depends on this.</summary>
        public static void SubmitRank(RankTrack track, Board result) => SendRank(new Dictionary<string, object>
        {
            ["track"] = track.ToString(), ["score"] = result.Score, ["placements"] = result.Placements, ["lines"] = result.LinesCleared,
        });

        /// <summary>A level win. The server keeps the best stars per level and pays only for progress, so replays cannot
        /// be ground for rank. (Needs the "Level" track in SubmitRank, see CloudCode/SUBMITRANK.md.)</summary>
        public static void SubmitRankLevel(int number, int stars) => SendRank(new Dictionary<string, object>
        {
            ["track"] = RankTrack.Level.ToString(), ["level"] = number, ["stars"] = stars,
        });

        /// <summary>A decided VS AI match (ties are not sent).</summary>
        public static void SubmitRankVs(BotLevel bot, bool won) => SendRank(new Dictionary<string, object>
        {
            ["track"] = RankTrack.VsAi.ToString(), ["bot"] = bot.ToString(), ["won"] = won,
        });

        private static async void SendRank(Dictionary<string, object> args)
        {
            try
            {
                if (IsPreview) return; // test builds never post to the live boards
                await EnsureReady();
                if (!Ready) return;
                var reply = await CloudCodeService.Instance.CallEndpointAsync<SubmitReply>("SubmitRank", args);
                if (!reply.accepted) Debug.LogWarning($"Rank rejected: {reply.reason}");
                await RefreshKingAsync();
            }
            catch (Exception e) { Debug.LogWarning($"Rank submit failed: {e.Message}"); }
        }

        public static async void RefreshKing()
        {
            try { await EnsureReady(); if (Ready && !IsPreview) await RefreshKingAsync(); }
            catch (Exception e) { Debug.LogWarning($"King rank failed: {e.Message}"); }
        }

        private static async Task RefreshKingAsync()
        {
            try
            {
                var me = await LeaderboardsService.Instance.GetPlayerScoreAsync(KingBoard);
                KingRank = me.Rank + 1;
            }
            catch (LeaderboardsException) { KingRank = 0; } // not on the board yet
        }

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
