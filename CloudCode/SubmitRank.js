/*
 * Cloud Code script: server-owned rank points for the King title (top 100 players).
 * The phone still keeps its own ladder for the progress bar, but King is decided here: points are computed from
 * the game the server was told about, capped and rate limited, kept in protected Cloud Save (players cannot write
 * it), and the total goes to the "King" leaderboard. A claimed "I have N points" is never accepted.
 *
 * What the server can and cannot check: Classic and Daily games are checked like SubmitScore (the Board rules must be
 * able to produce that score). A VS AI result (who won, which bot) cannot be proved, so it is capped instead:
 * a gap between submits, a per-day limit, and the same Easy/Medium caps as the game. This limits cheating, it does not end it.
 * Points rules mirror Assets/Scripts/Core/ModeRank.cs; keep the two in step.
 */
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");
const { DataApi } = require("@unity-services/cloud-save-1.4");

// Copied from SubmitScore.js on purpose: a Cloud Code script is deployed alone and cannot require a sibling file.
// SubmitRank.test.js fails if the two copies ever disagree.
const BOARDS = ["Classic", "Daily"];
const MAX_CELLS = 9;
const MAX_LINES_PER_PLACEMENT = 6;
const MAX_PLACEMENTS = 20000;
// Cells are the limit on lines: clearing a line removes cells that were placed, and the cheapest case
// (3 rows + 3 columns at once) still removes 39 cells for 6 lines, 6.5 per line. A piece adds at most 9 cells,
// and the board holds at most 64 to begin with, so 6.5 x lines <= 9 x placements + 64 (all doubled below).
const START_CELLS_ALLOWANCE = 64;

/** Returns null when the claim is possible under the Board rules, otherwise the reason it is not. */
function validate({ leaderboardId, score, placements, lines }) {
  if (!BOARDS.includes(leaderboardId)) return "unknown leaderboard";
  for (const [name, v] of [["score", score], ["placements", placements], ["lines", lines]])
    if (!Number.isSafeInteger(v) || v < 0) return `${name} must be a non-negative integer`;
  if (placements === 0 || placements > MAX_PLACEMENTS) return "placements out of range";
  if (lines > placements * MAX_LINES_PER_PLACEMENT) return "too many lines for the placements";
  if (13 * lines > 18 * placements + 2 * START_CELLS_ALLOWANCE) return "too many lines for the cells placed";
  // Every placement scores at least 1, and every cleared line at least 10.
  if (score < placements + 10 * lines) return "score too low for the placements and lines";
  // At most `lines` placements clear anything, so combo never exceeds that; each line is worth
  // at most 10 x 6 x (1 + combo).
  const clearing = Math.min(placements, lines);
  const max = MAX_CELLS * placements + 10 * MAX_LINES_PER_PLACEMENT * lines * clearing;
  if (score > max) return "score too high for the placements and lines";
  return null;
}

const KING_BOARD = "King";
const STATE_KEY = "rank_state";
const TRACKS = ["Classic", "Daily", "VsAi"];
const BOTS = ["Easy", "Medium", "Hard"];
const MIN_GAP_MS = 30000;      // two games cannot finish faster than this
const DAY_CAP = 1500;          // most points one player can earn per UTC day, across all modes
const EASY_CAP = 600, MEDIUM_CAP = 2800;
const TIER_STARTS = [0, 600, 1500, 2800, 4500, 6500, 8500, 11000];

const DAY_MS = 86400000;
const dayOf = (ms) => Math.floor(ms / DAY_MS);

function newState() {
  return { Classic: 0, Daily: 0, VsAi: 0, lastMs: 0, day: -1, dayGain: 0, dailyDone: -1, streak: 0, streakDay: -1, winStreak: 0 };
}

/** Start of the player's current rank: a VS AI loss never drops below it. */
function floorOf(points) {
  let f = 0;
  for (const s of TIER_STARTS) if (points >= s) f = s;
  return f;
}

/**
 * Pure rules. Returns { accepted:false, reason } or { accepted:true, gain, state } where state is the new record.
 * claim: { track, score?, placements?, lines?, bot?, won? }
 */
function applyGame(prev, claim, nowMs) {
  const s = Object.assign(newState(), prev);
  if (!TRACKS.includes(claim.track)) return { accepted: false, reason: "unknown track" };
  if (!Number.isFinite(nowMs) || nowMs < s.lastMs) return { accepted: false, reason: "clock went backwards" };
  if (nowMs - s.lastMs < MIN_GAP_MS) return { accepted: false, reason: "too soon after the last game" };

  const today = dayOf(nowMs);
  if (s.day !== today) { s.day = today; s.dayGain = 0; }
  if (s.dayGain >= DAY_CAP) return { accepted: false, reason: "daily rank limit reached" };

  let gain = 0;
  if (claim.track === "Classic" || claim.track === "Daily") {
    const problem = validate({ leaderboardId: claim.track, score: claim.score, placements: claim.placements, lines: claim.lines });
    if (problem) return { accepted: false, reason: problem };
    if (claim.track === "Classic") {
      gain = 15 + Math.min(135, Math.floor(claim.score / 30));
    } else {
      if (s.dailyDone === today) return { accepted: false, reason: "today's daily already counted" };
      // The server keeps the login streak itself; a claimed streak is never used.
      s.streak = s.streakDay === today - 1 ? s.streak + 1 : 1;
      s.streakDay = today;
      s.dailyDone = today;
      gain = 50 + Math.min(100, Math.floor(claim.score / 40)) + 5 * Math.min(10, s.streak);
    }
  } else {
    if (!BOTS.includes(claim.bot)) return { accepted: false, reason: "unknown bot" };
    if (typeof claim.won !== "boolean") return { accepted: false, reason: "won must be true or false" };
    if (claim.won) {
      const cap = claim.bot === "Easy" ? EASY_CAP : claim.bot === "Medium" ? MEDIUM_CAP : Infinity;
      const win = claim.bot === "Easy" ? 12 : claim.bot === "Medium" ? 25 : 40;
      gain = s.VsAi >= cap ? 0 : win + Math.min(10, 2 * s.winStreak);
      s.winStreak += 1;
    } else {
      const loss = claim.bot === "Easy" ? 15 : claim.bot === "Medium" ? 12 : 8;
      gain = 0 - Math.min(loss, s.VsAi - floorOf(s.VsAi));
      s.winStreak = 0;
    }
  }

  s[claim.track] += gain;
  if (gain > 0) s.dayGain += gain;
  s.lastMs = nowMs;
  return { accepted: true, gain, state: s };
}

const kingScore = (s) => s.Classic + s.Daily + s.VsAi;

module.exports = async ({ params, context, logger }) => {
  const claim = {
    track: params.track, score: params.score, placements: params.placements, lines: params.lines,
    bot: params.bot, won: params.won,
  };
  const cloudSave = new DataApi(context);
  const stored = await cloudSave.getProtectedItems(context.projectId, context.playerId, [STATE_KEY]);
  const item = stored.data && stored.data.results && stored.data.results[0];
  const result = applyGame(item ? item.value : null, claim, Date.now());
  if (!result.accepted) {
    logger.warning(`Rejected rank claim from ${context.playerId}: ${result.reason}`, { claim: JSON.stringify(claim) });
    return { accepted: false, reason: result.reason };
  }
  // Protected data can only be written by the server, so the phone cannot raise its own total.
  await cloudSave.setProtectedItem(context.projectId, context.playerId, { key: STATE_KEY, value: result.state });
  const total = kingScore(result.state);
  await new LeaderboardsApi(context).addLeaderboardPlayerScore(context.projectId, KING_BOARD, context.playerId, { score: total });
  return { accepted: true, gain: result.gain, points: { Classic: result.state.Classic, Daily: result.state.Daily, VsAi: result.state.VsAi }, king: total };
};

module.exports.params = {
  track: { type: "String", required: true },
  score: { type: "Numeric", required: false },
  placements: { type: "Numeric", required: false },
  lines: { type: "Numeric", required: false },
  bot: { type: "String", required: false },
  won: { type: "Boolean", required: false },
};

module.exports.applyGame = applyGame;
module.exports.validate = validate;
module.exports.kingScore = kingScore;
module.exports.DAY_CAP = DAY_CAP;
module.exports.MIN_GAP_MS = MIN_GAP_MS;
