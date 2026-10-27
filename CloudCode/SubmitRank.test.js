// Run with: node --test CloudCode/
const test = require("node:test");
const assert = require("node:assert");
const Module = require("node:module");

const load = Module._load;
Module._load = (req, ...rest) =>
  req === "@unity-services/leaderboards-1.1" ? { LeaderboardsApi: class {} }
  : req === "@unity-services/cloud-save-1.4" ? { DataApi: class {} }
  : load(req, ...rest);
const rank = require("./SubmitRank.js");
const score = require("./SubmitScore.js");
const { applyGame, DAY_CAP, MIN_GAP_MS } = rank;

const T0 = Date.UTC(2026, 9, 27, 12, 0, 0);
const classic = { track: "Classic", score: 18, placements: 2, lines: 1 };
const vs = (bot, won) => ({ track: "VsAi", bot, won });

test("score checks in SubmitRank match SubmitScore", () => {
  const cases = [
    { score: 18, placements: 2, lines: 1 }, { score: 1, placements: 1, lines: 0 }, { score: 999999999, placements: 10, lines: 3 },
    { score: 10, placements: 1, lines: 0 }, { score: 5, placements: 5, lines: 1 }, { score: -5, placements: 1, lines: 0 },
    { score: 0, placements: 0, lines: 0 }, { score: 1000, placements: 10, lines: 40 },
  ];
  for (const board of ["Classic", "Daily", "Hacked"])
    for (const c of cases) assert.strictEqual(rank.validate({ leaderboardId: board, ...c }), score.validate({ leaderboardId: board, ...c }));
});

test("classic game earns points from the validated score and is saved in the new state", () => {
  const r = applyGame(null, classic, T0);
  assert.strictEqual(r.accepted, true);
  assert.strictEqual(r.gain, 15);
  assert.strictEqual(r.state.Classic, 15);
  assert.strictEqual(rank.kingScore(r.state), 15);
});

test("an impossible score earns nothing", () => {
  const r = applyGame(null, { track: "Classic", score: 999999, placements: 2, lines: 1 }, T0);
  assert.strictEqual(r.accepted, false);
});

test("games faster than the gap are refused, later ones are fine", () => {
  const first = applyGame(null, classic, T0);
  assert.strictEqual(applyGame(first.state, classic, T0 + MIN_GAP_MS - 1).accepted, false);
  assert.strictEqual(applyGame(first.state, classic, T0 + MIN_GAP_MS).accepted, true);
});

test("a clock that goes backwards is refused", () => {
  const first = applyGame(null, classic, T0);
  assert.strictEqual(applyGame(first.state, classic, T0 - 1000).accepted, false);
});

test("daily counts once a day and the server keeps the streak", () => {
  const d = { track: "Daily", score: 400, placements: 60, lines: 5 };
  const a = applyGame(null, d, T0);
  assert.strictEqual(a.gain, 50 + 10 + 5);
  assert.strictEqual(applyGame(a.state, d, T0 + 60000).accepted, false);
  const next = applyGame(a.state, d, T0 + 86400000);
  assert.strictEqual(next.gain, 50 + 10 + 10);
  assert.strictEqual(next.state.streak, 2);
});

test("a claimed daily streak is ignored", () => {
  const r = applyGame(null, { track: "Daily", score: 400, placements: 60, lines: 5, streak: 99 }, T0);
  assert.strictEqual(r.gain, 65);
});

test("per-day cap stops farming, and the next day starts fresh", () => {
  let state = null, t = T0, accepted = 0;
  for (let i = 0; i < 200; i++) {
    const r = applyGame(state, vs("Hard", true), t);
    t += MIN_GAP_MS;
    if (!r.accepted) { assert.match(r.reason, /daily rank limit/); break; }
    state = r.state; accepted++;
  }
  assert.ok(state.VsAi >= DAY_CAP && state.VsAi < DAY_CAP + 60);
  assert.ok(accepted < 200);
  assert.strictEqual(applyGame(state, vs("Hard", true), Date.UTC(2026, 9, 28, 12, 0, 0)).accepted, true);
});

test("easy and medium bots stop paying past their caps", () => {
  assert.strictEqual(applyGame({ VsAi: 600, lastMs: 0 }, vs("Easy", true), T0).gain, 0);
  assert.strictEqual(applyGame({ VsAi: 599, lastMs: 0 }, vs("Easy", true), T0).gain, 12);
  assert.strictEqual(applyGame({ VsAi: 2800, lastMs: 0 }, vs("Medium", true), T0).gain, 0);
  assert.ok(applyGame({ VsAi: 5000, lastMs: 0 }, vs("Hard", true), T0).gain >= 40);
});

test("a loss costs but never drops below the start of the rank", () => {
  assert.strictEqual(applyGame({ VsAi: 1000, lastMs: 0 }, vs("Easy", false), T0).gain, -15);
  assert.strictEqual(applyGame({ VsAi: 605, lastMs: 0 }, vs("Easy", false), T0).gain, -5);
  assert.strictEqual(applyGame({ VsAi: 600, lastMs: 0 }, vs("Easy", false), T0).gain, 0);
  assert.strictEqual(applyGame({ VsAi: 3, lastMs: 0 }, vs("Hard", false), T0).gain, -3);
});

test("win streak adds up to ten and a loss resets it", () => {
  let r = applyGame(null, vs("Hard", true), T0);
  assert.strictEqual(r.gain, 40);
  r = applyGame(r.state, vs("Hard", true), T0 + MIN_GAP_MS);
  assert.strictEqual(r.gain, 42);
  r = applyGame(r.state, vs("Hard", false), T0 + 2 * MIN_GAP_MS);
  r = applyGame(r.state, vs("Hard", true), T0 + 3 * MIN_GAP_MS);
  assert.strictEqual(r.gain, 40);
});

test("malformed claims are refused", () => {
  assert.strictEqual(applyGame(null, { track: "Hacked" }, T0).accepted, false);
  assert.strictEqual(applyGame(null, { track: "VsAi", bot: "God", won: true }, T0).accepted, false);
  assert.strictEqual(applyGame(null, { track: "VsAi", bot: "Easy", won: "yes" }, T0).accepted, false);
});
