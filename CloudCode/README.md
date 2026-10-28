# Server-side score checks

`SubmitScore.js` is a Unity Cloud Code script. The game sends each finished Classic or Daily score to it
with the number of pieces placed and lines cleared; it rejects scores the Board rules cannot produce (including more lines than the cells placed could clear) and
writes the rest to the leaderboard. `project-policy.json` stops players writing leaderboard scores directly,
so the script is the only way in. This closes audit finding V1 for obviously impossible scores; it does not
replay games, so a cheater who sends consistent numbers can still post a high but possible score.

## Is it live right now? (2 minutes, browser only)
1. Open the Unity Cloud dashboard, choose the Block Drop project, and switch the environment (top left) to **production**.
2. Go to **Development > Products > Cloud Code > JS Scripts**.
3. If you see **SubmitScore** with a published version, the script is live. Open it and check the parameters are
   `leaderboardId`, `score`, `placements`, `lines`. If the list is empty or has no SubmitScore, it is **not live**, and
   every score from the 1.0.0 game is being dropped. Follow the manual path below.
4. Proof from the game: finish one Classic game on a phone with the 1.0.0 build, then open **RANKINGS**. Your score
   should appear. Cloud Code > Logs shows "Rejected score" lines if a score was refused.

The workflow in `.github/workflows/deploy-score-check.yml` also has a read-only `check` step that prints the same answer.
GitHub only shows a workflow on the **Actions** tab once it is on the default branch, so until this is merged, use the
dashboard.

## Switch it on (browser only, no Mac needed)
1. In the Unity Cloud dashboard go to **Administration > Service accounts**, create one for this project, give it
   the Cloud Code and Access Control editor roles plus environment viewer, and create a key.
2. In GitHub go to **Settings > Secrets and variables > Actions** and add the key as `UGS_KEY_ID` and `UGS_SECRET_KEY`.
3. In GitHub **Actions > Deploy score check (Unity Cloud) > Run workflow**, choose `script`. This publishes
   `SubmitScore` to the production environment.
4. Ship the game build that includes this change and check one finished game shows up on the leaderboard.
5. Run the workflow again with `policy`. From then on players can only submit scores through the script.
   Doing this before step 4 is live would stop every score from being saved.

Without a service account, step 3 can also be done by hand: **Development > Products > Cloud Code > JS Scripts**,
production environment, create `SubmitScore`, add parameters `leaderboardId` (String), `score`, `placements` and
`lines` (Numeric), all required, paste `SubmitScore.js`, then **Publish version**. Step 5 has no dashboard page and
needs the workflow (or the UGS CLI).

## Test the rules locally
`node --test CloudCode/SubmitScore.test.js CloudCode/Wallet.test.js`
