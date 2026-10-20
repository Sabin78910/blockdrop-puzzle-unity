# Server-side score checks

`SubmitScore.js` is a Unity Cloud Code script. The game sends each finished Classic or Daily score to it
with the number of pieces placed and lines cleared; it rejects scores the Board rules cannot produce and
writes the rest to the leaderboard. `project-policy.json` stops players writing leaderboard scores directly,
so the script is the only way in. This closes audit finding V1 for obviously impossible scores; it does not
replay games, so a cheater who sends consistent numbers can still post a high but possible score.

The Levels board ranks total level stars (the script only range-checks that value; stars come from client progress).
Create the `Levels` leaderboard in the Unity dashboard first: sort descending, update strategy keep best, no reset.

## Deploy (once, from a terminal with the [UGS CLI](https://services.docs.unity.com/guides/ugs-cli/latest/general/overview))
1. `ugs login`, then `ugs config set project-id <your project id>` and `ugs config set environment-name production`.
2. `ugs deploy CloudCode/SubmitScore.js` (publishes the script as `SubmitScore`).
3. Ship the game build that calls `SubmitScore`.
4. Then `ugs access upsert-project-policy CloudCode/project-policy.json` to block direct writes.
   Do this after step 3 is live, or older builds lose leaderboard submissions.

## Test the rules locally
`node --test CloudCode/SubmitScore.test.js`
