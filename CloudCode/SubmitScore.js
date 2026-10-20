/*
 * Cloud Code script: the only way scores reach the leaderboards.
 * Players are denied direct leaderboard writes (see project-policy.json), so the game calls this
 * script with the score plus how many pieces it placed and lines it cleared. Scores that the
 * Board rules could not produce from those counts are rejected.
 *
 * Board rules (Assets/Scripts/Core/Board.cs): a placement earns 1 point per cell (1 to 9 cells)
 * plus 10 x lines^2 x (1 + combo) when it clears lines; one placement clears at most 6 lines.
 */
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");

const BOARDS = ["Classic", "Daily", "Levels"];
const MAX_LEVEL_STARS = 30000; // the Levels board ranks total stars; three per level
const MAX_CELLS = 9;
const MAX_LINES_PER_PLACEMENT = 6;
const MAX_PLACEMENTS = 20000;

/** Returns null when the claim is possible under the Board rules, otherwise the reason it is not. */
function validate({ leaderboardId, score, placements, lines }) {
  if (!BOARDS.includes(leaderboardId)) return "unknown leaderboard";
  if (leaderboardId === "Levels") {
    // Stars come from level play, not Board counts: only the range can be checked here.
    return Number.isSafeInteger(score) && score >= 0 && score <= MAX_LEVEL_STARS ? null : "stars out of range";
  }
  for (const [name, v] of [["score", score], ["placements", placements], ["lines", lines]])
    if (!Number.isSafeInteger(v) || v < 0) return `${name} must be a non-negative integer`;
  if (placements === 0 || placements > MAX_PLACEMENTS) return "placements out of range";
  if (lines > placements * MAX_LINES_PER_PLACEMENT) return "too many lines for the placements";
  // Every placement scores at least 1, and every cleared line at least 10.
  if (score < placements + 10 * lines) return "score too low for the placements and lines";
  // At most `lines` placements clear anything, so combo never exceeds that; each line is worth
  // at most 10 x 6 x (1 + combo).
  const clearing = Math.min(placements, lines);
  const max = MAX_CELLS * placements + 10 * MAX_LINES_PER_PLACEMENT * lines * clearing;
  if (score > max) return "score too high for the placements and lines";
  return null;
}

module.exports = async ({ params, context, logger }) => {
  const claim = {
    leaderboardId: params.leaderboardId,
    score: params.score,
    placements: params.placements,
    lines: params.lines,
  };
  const problem = validate(claim);
  if (problem) {
    logger.warning(`Rejected score from ${context.playerId}: ${problem}`, { claim: JSON.stringify(claim) });
    return { accepted: false, reason: problem };
  }
  // Built from the context, the API writes with server authority, which the player policy allows.
  const api = new LeaderboardsApi(context);
  await api.addLeaderboardPlayerScore(context.projectId, claim.leaderboardId, context.playerId, { score: claim.score });
  return { accepted: true };
};

module.exports.params = {
  leaderboardId: { type: "String", required: true },
  score: { type: "Numeric", required: true },
  placements: { type: "Numeric", required: true },
  lines: { type: "Numeric", required: true },
};

module.exports.validate = validate;
