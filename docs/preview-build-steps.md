# Preview build of all pending updates (Android APK, installs next to the Play version)

Everything is in the browser. The branch `preview/first-update` holds the endless levels (#16), level map (#17), new Rankings screen (#18) and the praise, guide, HUD and Home button changes (#19) plus the gentler level ramp (#20), map polish (#21) the level-complete celebration (#22) and the simpler home screen (#23), in one commit on top of `main`. Nothing on `main`, the open PRs or the "Play Release" target changes.

## 1. Add the Preview build target
1. Open cloud.unity.com > Block Drop Puzzle > **DevOps > Build Automation**.
2. Press **New build target**.
3. Fill in:
   - **Name:** `Preview`
   - **Platform:** Android
   - **Source branch:** `preview/first-update`
   - **Builder and Unity version:** same as "Play Release" (6000.6.2f1)
   - **Build on commit:** **off**. You press Build yourself.
   - **Pre-export method:** `BlockDrop.EditorTools.BuildScript.PreExportPreview`
   - **Credentials / keystore:** leave empty. **Do not** pick the Play upload key.
4. Save.
5. Check "Play Release" still has **Pre-export method** `BlockDrop.EditorTools.BuildScript.PreExportAppBundle`. Don't edit it.

## 2. Build it
1. Open the **Preview** target and press **Build**.
2. When it succeeds, download the **.apk** (not an .aab).
   **If it fails:** copy the first red error line from the build log into the Block Drop thread. It is probably a compile error, because the new screen code has not been through a real Unity build yet.

## 3. Install on your phone
1. Send the .apk to your phone (Drive, email or cable) and open it. Allow "install unknown apps" if Android asks.
2. You get a second icon called **Block Drop Preview**. The Play version is untouched.

## 4. What to look at
- Home shows only Play, Levels, Daily, Missions and Themes; everything else is under **Settings**.
- Win a level: a headline above the card (and CHAPTER n COMPLETE! on every 10th level), confetti, coins counting up.
- First launch: the Blocky guide shows in Classic with a **SKIP** button. Place a piece, clear a line, make a combo, and watch the praise words get bigger.
- A level: score plate, "NEXT ★ IN n", and a "!" when 3 or fewer moves are left.
- Finish a level: **HOME** is next to **LEVELS**. Game Over also says HOME.
- Levels is the winding map; Rankings has the new rows. Try a long name.
- Send screenshots of anything cramped or under the notch to the Block Drop thread.

## Will the preview clash with the release?
- **App id:** preview installs as `com.sabin.blockdrop.preview`, so it never replaces the Play app `com.sabin.blockdrop`. Both sit on one phone.
- **Keystore:** the Preview target does not use the Play upload key, so it can't be uploaded to Play by mistake.
- **Build number:** preview uses Unity's own build number as its version code, so each new preview installs over the last one. The Play version code stays 1.
- **Saves:** separate from the Play app. Preview starts as a new player.
- **Leaderboard:** preview builds do not submit scores, so test games never reach the live rankings. The Rankings screen still reads the live list.

## Watch out for
- Don't set `PreExportPreview` on "Play Release", or `PreExportAppBundle` on Preview.
- Preview builds from `preview/first-update`, not `main`. After #16 to #19 are merged, delete the Preview target or point it at `main`.
- Preview shares the live Unity services project, so each install adds an anonymous test player.
