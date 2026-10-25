using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;
#if BLOCKDROP_GPGS
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace BlockDrop.Game
{
    /// <summary>"Continue with Google": ties the anonymous Unity player to the player's Google Play Games account, so a
    /// reinstall or new phone signs back in to the same player and <see cref="CloudSync"/> restores progress.
    /// Compiled only when the scripting define BLOCKDROP_GPGS is set (after the Google Play Games plugin and
    /// its Web client ID are set up, see CloudCode/ACCOUNTS.md); otherwise <see cref="Available"/> is false and nothing runs.</summary>
    public static class AccountLink
    {
#if BLOCKDROP_GPGS
        public static bool Available => Application.platform == RuntimePlatform.Android;
        public static bool Linked => OnlineService.Ready && AuthenticationService.Instance.PlayerInfo?.GetGooglePlayGamesId() != null;

        /// <summary>Returns null on success, otherwise a short message safe to show the player.</summary>
        public static async Task<string> ContinueAsync()
        {
            try
            {
                await OnlineService.EnsureReady();
                if (!OnlineService.Ready) return "You're offline. Try again when you're connected.";
                if (!await AuthenticateAsync()) return "Google sign-in was cancelled.";

                string code = await ServerCodeAsync();
                if (code == null) return "Couldn't get a Google sign-in code.";
                try { await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(code); }
                catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                {
                    // This Google account already belongs to a player (an earlier install): switch to that player.
                    code = await ServerCodeAsync();
                    if (code == null) return "Couldn't get a Google sign-in code.";
                    AuthenticationService.Instance.SignOut();
                    await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(code);
                    await OnlineService.AfterAccountChangeAsync();
                }
                await CloudSync.SyncAsync();
                return null;
            }
            catch (Exception e) { Debug.LogWarning("Google link failed: " + e.Message); return "Couldn't connect your Google account. Please try again."; }
        }

        private static Task<bool> AuthenticateAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            PlayGamesPlatform.Activate();
            PlayGamesPlatform.Instance.Authenticate(status => tcs.TrySetResult(status == SignInStatus.Success));
            return tcs.Task;
        }

        private static Task<string> ServerCodeAsync()
        {
            var tcs = new TaskCompletionSource<string>();
            PlayGamesPlatform.Instance.RequestServerSideAccess(true, code => tcs.TrySetResult(string.IsNullOrEmpty(code) ? null : code));
            return tcs.Task;
        }
#else
        public static bool Available => false;
        public static bool Linked => false;
        public static Task<string> ContinueAsync() => Task.FromResult("Google sign-in isn't set up in this build.");
#endif
    }
}
