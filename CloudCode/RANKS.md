# Ranks and the King title

The ladder (Bronze V up to Legend) lives on the phone and in the cloud save (`PlayerMeta`, merged by `CloudProgress`).
That is fine for a personal progress bar, but not for a public title.

**King = the top 100 players** is a comparison between players, so it cannot be decided on a phone, and the points are
client-submitted today (audit finding V1). To make King real:

1. Create a Unity Leaderboards board `Rank` (sort descending, keep best, no reset).
2. Add a Cloud Code script `SubmitRank`, like `SubmitScore.js`, that is the only writer of that board
   (add it to `project-policy.json`). It must not accept a bare "my RP is N" claim, or anyone can post 99,999.
   Either it recomputes RP from each validated game result (the same placements/lines checks as `SubmitScore`) and
   keeps the running total in server-side Cloud Save, or it only accepts RP growth that the last 24 hours of
   validated games could have produced.
3. The game submits after each ranked game, only for players at Legend (11,000 RP), and shows KING when the
   player's server rank is 100 or better. The title is recomputed on each read and can be lost.
4. Deploy the script and board in the UGS dashboard (needs the service account from `README.md`).

Until then the game shows Legend as the top rank. Nothing in the client claims King.
