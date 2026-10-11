# Block Drop – UAT & VAPT report

**Build:** 1.0.0 (versionCode 1) · Unity 6000.6.2f1 · IL2CPP ARM64 · target API 36
**Date:** 9 Oct 2026 · **Device:** Android emulator (Pixel, API 36) + owner's phone
**Automated tests:** 30/30 EditMode tests pass

## 1. UAT (user acceptance testing)

| # | Scenario | Result |
|---|---|---|
| 1 | First launch: daily-streak popup, Collect adds coins | ✅ Pass |
| 2 | First-run tutorial: drag hint → "clear a line" step | ✅ Pass |
| 3 | Classic / Daily: place, clear, score, combo streak meter, shatter FX | ✅ Pass |
| 4 | First trophy toast ("★ FIRST CLEAR +10"), XP bar moves | ✅ Pass |
| 5 | Back button / MENU during a game | ❌→✅ **Fixed**: used to discard the game; now shows a Pause screen (Resume / Quit) |
| 6 | Daily missions progress (lines, score) | ✅ Pass |
| 7 | Trophies screen (12 trophies, progress, DONE) | ✅ Pass |
| 8 | Themes: buying without enough coins is refused | ✅ Pass |
| 9 | Levels: level 1 HUD (moves, stars), play to "OUT OF MOVES" result | ✅ Pass |
| 10 | VS AI: AI takes its turn on the mini board | ✅ Pass |
| 11 | 2 Players: turn display | ✅ Pass |
| 12 | Challenge code: invalid code shows help, valid code starts the game | ❌→✅ **Fixed**: with the phone keyboard open, PLAY CODE didn't respond; the keyboard's Done key now submits, letters forced to capitals |
| 13 | Player name: save to online profile ("Saved! You are Tester_1#79410") | ❌→✅ **Fixed**: same keyboard issue as #12 |
| 14 | Rankings: Classic and Daily load ("No scores yet. Be the first!") | ✅ Pass (after leaderboard ID fix) |
| 15 | Offline launch: game fully playable, clear offline message in Rankings | ✅ Pass |
| 16 | Network returns while the game is open | ❌→✅ **Fixed**: game stayed OFFLINE until restart (sign-in retry bug); now reconnects within seconds and uploads a name chosen offline |
| 17 | Force-close the app mid-game, reopen: coins, XP, trophies kept | ❌→✅ **Fixed earlier**: progress is saved on every reward and on going to background |
| 18 | Menu "MISSIONS (2!)" label clipped on phones | ❌→✅ **Fixed**: red number badge |
| – | 40-minute break reminder | ⏭ Not run on device (time-based); code reviewed |
| – | Buying a theme with enough coins, claiming a mission, Master-AI trophy | ⏭ Covered by unit tests (`MetaTests`), not run by hand |

## 2. VAPT (vulnerability assessment)

**Scope:** the Android app (APK and release AAB), its source repository, and its use of Unity Gaming Services. No active testing against Unity's servers (third-party, not authorised).

### Passed checks
- Permissions are minimal: `INTERNET`, `ACCESS_NETWORK_STATE`, `VIBRATE`.
- Not debuggable; cleartext HTTP is off (target API 36 default); all traffic to Unity uses HTTPS.
- Only the launcher activity is exported; the content provider is not exported.
- No secrets: none in the code, none in git history, none inside the APK/AAB; no keystore files in the repo.
- The release AAB is signed with the upload key (CN=Sabin Khanal); Google re-signs via Play App Signing.
- Native IL2CPP code (harder to tamper with than Mono).
- **CVE-2025-59489** (Unity runtime argument injection, CVSS 8.4): affected editors up to 6000.3; this build uses 6000.6. Live test: launching the app with `-xrsdk-pre-init-library /data/local/tmp/evil.so` did not load the library, and the app started normally.
- The privacy policy matches what the app sends (anonymous ID, scores, optional nickname).

### Findings
| ID | Severity | Finding | Status / recommendation |
|---|---|---|---|
| V1 | Medium | Leaderboard scores are sent by the app itself, so a modified app could post fake scores. | Open. Before scaling: validate scores server-side with Unity Cloud Code (plausibility limits per game) and block direct client writes. |
| V2 | Low | Players can choose public nicknames; there is no profanity filter or report option. | **Fixed 11 Oct 2026:** `NameFilter` blocks offensive names on save (English, Spanish, Portuguese, romanised Nepali; sees through leetspeak and separators) and the leaderboard shows other players' offensive names as `Player#1234`. 10 new EditMode tests. A report button is still open. |
| V3 | Low | Coins, XP and trophies are stored unencrypted on the phone and can be edited on a rooted device. | Accepted: cosmetic only, no purchases and no effect on other players. |
| V4 | Info | Android Auto Backup is on, so progress can be restored to a new phone via Google backup (`adb backup` is blocked for target API 36). | Accepted: this is a benefit. |
| V5 | Info | HawkScan (DAST) is for web apps and needs a StackHawk API key. The game has no server of its own. | Not applicable; can be run later against the Render APIs. |
