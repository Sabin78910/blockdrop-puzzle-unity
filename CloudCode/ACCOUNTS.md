# Saved progress and Continue with Google

## What works without any setup
`CloudSync` saves level stars, coins, themes, achievements and best score to Unity Cloud Save for the signed-in
player, restores them when a phone has less than the cloud copy, and merges rather than overwrites.
It needs the Cloud Save service on in the Unity Cloud dashboard (Development > Products > Cloud Save), nothing else.

This alone restores progress when the same player signs in again. Anonymous players lose that identity on a
reinstall, which is why Continue with Google exists.

## Continue with Google (needs setup, then it works from the settings screen)
`AccountLink` is compiled only when the scripting define `BLOCKDROP_GPGS` is set, so builds are safe until then.

1. **Play Console** > your app > Grow > Play Games Services > Setup and management > Configuration. Create the Play
   Games Services project, then under Credentials add:
   - an **Android** OAuth client for package `com.sabin.blockdrop` using the SHA-1 of the Play app signing key
     (Test and release > App integrity), and a second one for the upload key;
   - a **Game server (web)** OAuth client. Keep its client ID and secret.
2. **Unity Cloud dashboard** > Authentication > Identity providers > Add > Google Play Games: paste the web client ID and secret.
3. **Unity Editor** (needs the editor once): import the Google Play Games plugin for Unity v11.01 or newer, open
   Window > Google Play Games > Setup > Android, fill in the app ID and the web client ID, then add `BLOCKDROP_GPGS`
   under Project Settings > Player > Android > Scripting Define Symbols.
4. Add a button that calls `AccountLink.ContinueAsync()` (shows nothing while `AccountLink.Available` is false).

If this Google account was already linked from an earlier install, `ContinueAsync` signs in as that player and
`CloudSync` restores the progress.
