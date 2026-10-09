using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
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
        // Leaderboard IDs must exist in the Unity Cloud dashboard (Leaderboards → Create).
        public const string ClassicBoard = "classic";
        public const string DailyBoard = "daily";

        public struct Row { public int Rank; public string Name; public long Score; public bool IsMe; }

        public static bool Ready { get; private set; }
        public static string Status { get; private set; } = "Connecting…";
        public static string PlayerName { get; private set; } = "";
        public static readonly Dictionary<string, List<Row>> Top = new Dictionary<string, List<Row>>();
        public static readonly Dictionary<string, string> Errors = new Dictionary<string, string>();

        private static Task _init;

        public static Task EnsureReady() => _init ??= InitAsync();

        private static async Task InitAsync()
        {
            try
            {
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    Status = "Offline — playing without leaderboards";
                    _init = null; // retry later
                    return;
                }
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                PlayerName = await AuthenticationService.Instance.GetPlayerNameAsync(); // auto-generated, e.g. "SwiftFox#1234"
                Ready = true;
                Status = "Online";
            }
            catch (Exception e)
            {
                Status = "Offline — playing without leaderboards";
                Debug.LogWarning("Online services unavailable: " + e.Message);
                _init = null;
            }
        }

        public static async void SubmitScore(string board, int score)
        {
            try
            {
                await EnsureReady();
                if (!Ready || score <= 0) return;
                await LeaderboardsService.Instance.AddPlayerScoreAsync(board, score);
                await RefreshAsync(board);
            }
            catch (Exception e) { Debug.LogWarning($"Score submit failed ({board}): {e.Message}"); }
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
                rows.Add(new Row { Rank = e.Rank + 1, Name = e.PlayerName, Score = (long)e.Score, IsMe = e.PlayerId == AuthenticationService.Instance.PlayerId });
            Top[board] = rows;
        }
    }
}
