# Ranks and the King title

The ladder (Bronze V up to Legend) lives on the phone and in the cloud save (`PlayerMeta`, merged by `CloudProgress`).
That is fine for a personal progress bar, but not for a public title.

**King = the top 100 players** is a comparison between players, so it cannot be decided on a phone, and the points are
client-submitted today (audit finding V1). Not built yet. To make King real:

1. **Board.** Create a Unity Leaderboards board `Rank` (sort descending, keep best, no reset) and add it to
   `project-policy.json` with client writes denied, like the score boards.
2. **Script.** Add a Cloud Code script `SubmitRank`, the only writer of that board. Move the validation in
   `SubmitScore.js` into a shared module that both scripts import, rather than copying it.
3. **Server-owned total.** Keep the server's RP total out of `blockdrop_save`, which the client can write and
   `CloudSync` merges. Store it under its own Cloud Save key that only `SubmitRank` can write. The phone's rank bar
   stays local; King reads only the server value.
4. **What the server can and cannot prove.** For Classic and Daily it can recompute RP from the validated score
   (placements/lines checks). For VS AI it cannot: the checks prove a score is plausible, not who won, so win/loss
   RP stays client-claimed. Limit the damage with server-side caps on RP per game, per hour and per 24 hours, plus
   a per-player rate limit. This limits cheating, it does not eliminate it.
5. **Linked account.** Require a linked Google account for King (`AccountLink` already exists), because anonymous
   ids are free to farm.
6. **Client.** After each ranked game, only players at Legend (11,000 RP) call `SubmitRank`. The game shows KING
   when the player's server rank is 100 or better, recomputed on each read, so the title can be lost.
7. **Deploy.** Publish the script and board in the UGS dashboard (needs the service account from `README.md`).
   Cost is negligible, since only Legend players call it.

Until then the game shows Legend as the top rank. Nothing in the client claims King.
