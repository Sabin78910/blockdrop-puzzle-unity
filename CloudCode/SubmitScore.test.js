// Run with: node --test CloudCode/
const test = require("node:test");
const assert = require("node:assert");
const Module = require("node:module");

// The Leaderboards SDK only exists inside Cloud Code; stub it so the rules can be tested locally.
const load = Module._load;
Module._load = (req, ...rest) => (req === "@unity-services/leaderboards-1.1" ? { LeaderboardsApi: class {} } : load(req, ...rest));
const { validate } = require("./SubmitScore.js");

const ok = (c) => assert.strictEqual(validate({ leaderboardId: "Classic", ...c }), null);
const bad = (c) => assert.notStrictEqual(validate({ leaderboardId: "Classic", ...c }), null);

test("accepts real games from the Board tests", () => {
  ok({ score: 18, placements: 2, lines: 1 }); // two 4-cell lines clearing one row
  ok({ score: 1, placements: 1, lines: 0 });
});

test("rejects unknown boards and malformed numbers", () => {
  assert.notStrictEqual(validate({ leaderboardId: "Hacked", score: 1, placements: 1, lines: 0 }), null);
  bad({ score: -5, placements: 1, lines: 0 });
  bad({ score: 1.5, placements: 1, lines: 0 });
  bad({ score: "10", placements: 1, lines: 0 });
  bad({ score: 0, placements: 0, lines: 0 });
});

test("rejects scores the rules cannot reach", () => {
  bad({ score: 999999999, placements: 10, lines: 3 });
  bad({ score: 10, placements: 1, lines: 0 }); // one piece is at most 9 points
  bad({ score: 5, placements: 5, lines: 1 }); // a cleared line is worth at least 10
  bad({ score: 100, placements: 2, lines: 13 }); // two placements clear at most 12 lines
});

test("accepts the best possible single placement", () => {
  ok({ score: 9 + 10 * 36, placements: 1, lines: 6 });
});

test("rejects more lines than the cells placed could ever clear", () => {
  bad({ score: 100000, placements: 10, lines: 30 }); // 10 pieces add 90 cells, 30 lines remove at least 195
  ok({ score: 9 * 10 + 10 * 8 * 8 * 5, placements: 10, lines: 8 }); // dense but possible: 8 lines from 10 pieces
});
