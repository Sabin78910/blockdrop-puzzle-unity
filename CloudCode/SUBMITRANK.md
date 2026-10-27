# SubmitRank: server-owned points for the King title

King = top 100 players, so the points that decide it live on the server. `SubmitRank.js` is the only writer of the
`King` leaderboard (player writes are already denied for every board by `project-policy.json`).

## What it does
- Keeps one record per player in **protected Cloud Save** (key `rank_state`, players cannot write protected data).
  The phone's own ladder in `PlayerMeta` / `blockdrop_save` is only a progress bar and never feeds King.
- Classic and Daily: the claimed score must pass the same Board-rules check as `SubmitScore` (a copy of it, with a test
  that fails if the two drift). Points: `ModeRank.ClassicGain` and `DailyGain`; the server keeps the Daily streak itself.
- VS AI: the result cannot be proved, so it is capped: 30 s between games, 1,500 points per UTC day, Easy and Medium stop
  paying past 600 and 2,800 like the game, and a loss still costs.
- King score on the board = Classic + Daily + VS AI totals (sort descending, keep best, no reset).
- Honest limit: this slows cheating, it does not end it. A determined cheater can report wins up to the daily cap.

## Calling it (client side, ranks thread)
`CloudCode.CallEndpoint`-style call to `SubmitRank` with `track` ("Classic", "Daily" or "VsAi") plus
`score`, `placements`, `lines` for Classic/Daily, or `bot` ("Easy", "Medium", "Hard") and `won` for VsAi.
Reply: `{accepted, gain, points:{Classic,Daily,VsAi}, king}` or `{accepted:false, reason}`.
Show KING only when the player is Google-linked (`AccountLink.Linked`) and `GetPlayerScore` on `King` gives rank 100 or better.
The server cannot see whether an account is linked, so that rule is enforced in the client only.

## Deploy (needs leo or the service account)
1. Unity Cloud, Leaderboards: create board `King` (descending, keep best, no reset).
2. Cloud Code: new script `SubmitRank`, paste `CloudCode/SubmitRank.js`, Publish.
3. Test: `node --test CloudCode/SubmitScore.test.js CloudCode/SubmitRank.test.js`.

## Not verified
The rules are tested locally. The Cloud Save calls (`getProtectedItems`, `setProtectedItem` from
`@unity-services/cloud-save-1.4`) and the live script have not run, so check them with one real call after publishing.
