/*
 * Cloud Code script: the coin wallet for expensive themes (Legendary and up, 5,000 coins and more).
 * The phone keeps playing offline and counts what it earned and spent; this script is the only place that decides
 * the balance and who owns the server-sold themes. Both live in protected Cloud Save, which players can read
 * but not write, so editing PlayerPrefs on the phone changes nothing the server believes.
 *
 * What the server can and cannot check: a coin reward is not provable (the game is not replayed), so earnings are
 * rate limited instead. Each sync can credit at most what a token bucket holds: a heavy honest day times the player's
 * Fortune multiplier, refilled continuously. A cheater is limited to that rate, not stopped. Spending is checked exactly:
 * a theme is bought only if the server-side balance covers the catalog price and the ladder rule allows it.
 * Under-reporting spend on boosters or continues keeps a few coins; it cannot reach a theme faster than the earn cap.
 * Catalog, Fortune and the ladder mirror Assets/Scripts/Core/Meta.cs and Economy.cs; Wallet.test.js fails if they drift.
 */
const { DataApi } = require("@unity-services/cloud-save-1.4");

const SERVER_MIN_PRICE = 5000;
const FORTUNE_BASE = 9000;
const STATE_KEY = "wallet";
const DAY_MS = 86400000;
const DAY_EARN = 4000;          // base coins a very active player can honestly earn in a day (before Fortune)
const BUCKET_DAYS = 3;          // the bucket holds three such days, so a weekend binge is not clipped
const FIRST_SYNC_ALLOWANCE = 40000; // one time: lets a save from before the wallet bring its coins across
const LEGACY_MAX_PRICE = 9000;  // themes bought before the wallet existed (Legendary and below) are honoured once

// Server-sold themes only. tier is the ladder tier; requires is an extra theme that must be owned.
const CATALOG = [
  { name: "Aurora", price: 5000, tier: "Legendary" },
  { name: "Galaxy", price: 6500, tier: "Legendary" },
  { name: "Diamond", price: 8000, tier: "Legendary" },
  { name: "Royal Gold", price: 9000, tier: "Legendary" },
  { name: "Ember", price: 15000, tier: "Mythic" },
  { name: "Tidal", price: 40000, tier: "Mythic" },
  { name: "Orchid", price: 100000, tier: "Mythic" },
  { name: "Obsidian", price: 250000, tier: "Mythic" },
  { name: "Nebula", price: 500000, tier: "Cosmic" },
  { name: "Supernova", price: 1000000, tier: "Cosmic" },
  { name: "Quasar", price: 5000000, tier: "Cosmic" },
  { name: "Pulsar", price: 25000000, tier: "Cosmic" },
  { name: "Aether", price: 100000000, tier: "Eternal" },
  { name: "Genesis", price: 250000000, tier: "Eternal" },
  { name: "Singularity", price: 1000000000, tier: "Eternal", requires: "Genesis" },
];
const TIER_BELOW = { Mythic: "Legendary", Cosmic: "Mythic", Eternal: "Cosmic" };
const bySName = (n) => CATALOG.find((t) => t.name === n);

function newState() {
  return { earned: 0, spent: 0, repEarned: 0, repSpent: 0, owned: [], bucket: 0, bucketMs: 0, started: false };
}

/** Coin reward multiplier: the best owned theme's price over 9,000, never below 1. */
function fortune(owned) {
  let best = 0;
  for (const n of owned) { const t = bySName(n); if (t) best = Math.max(best, t.price); }
  return Math.max(1, best / FORTUNE_BASE);
}

const balanceOf = (s) => Math.max(0, s.earned - s.spent);

function isCount(v) { return Number.isSafeInteger(v) && v >= 0; }

/**
 * Applies the phone's lifetime counters. claim: { earned, spent, legacyOwned?: [names] }.
 * Returns { ok:false, reason } or { ok:true, state, credited }.
 */
function sync(prev, claim, nowMs) {
  const s = Object.assign(newState(), prev, { owned: [...((prev && prev.owned) || [])] });
  if (!claim || !isCount(claim.earned) || !isCount(claim.spent)) return { ok: false, reason: "counters must be non-negative integers" };
  if (!Number.isFinite(nowMs) || nowMs < s.bucketMs) return { ok: false, reason: "clock went backwards" };

  const mult = fortune(s.owned);
  const cap = DAY_EARN * BUCKET_DAYS * mult;
  if (!s.started) {
    s.started = true;
    s.bucket = cap + FIRST_SYNC_ALLOWANCE;
    // Themes bought on the phone before the wallet existed, once, and only what the old game could sell.
    for (const n of Array.isArray(claim.legacyOwned) ? claim.legacyOwned : []) {
      const t = bySName(n);
      if (t && t.price <= LEGACY_MAX_PRICE && !s.owned.includes(n)) s.owned.push(n);
    }
  } else {
    s.bucket = Math.min(cap, s.bucket + ((nowMs - s.bucketMs) / DAY_MS) * DAY_EARN * mult);
  }
  s.bucketMs = nowMs;

  // Counters only move forward: a smaller number is a stale or tampered copy and changes nothing.
  const dEarn = Math.max(0, claim.earned - s.repEarned);
  const credited = Math.min(dEarn, Math.floor(s.bucket));
  s.bucket -= credited;
  s.earned += credited;
  s.repEarned = Math.max(s.repEarned, claim.earned);

  const dSpent = Math.max(0, claim.spent - s.repSpent);
  s.spent += Math.min(dSpent, balanceOf(s)); // never below zero
  s.repSpent = Math.max(s.repSpent, claim.spent);
  return { ok: true, state: s, credited };
}

/** Returns { ok:false, reason } or { ok:true, state } with the theme owned and its price spent. */
function buy(prev, themeName) {
  const s = Object.assign(newState(), prev, { owned: [...((prev && prev.owned) || [])] });
  const t = bySName(themeName);
  if (!t) return { ok: false, reason: "not sold by the server" };
  if (s.owned.includes(t.name)) return { ok: false, reason: "already owned" };
  const below = TIER_BELOW[t.tier];
  if (below && !s.owned.some((n) => (bySName(n) || {}).tier === below)) return { ok: false, reason: `own a ${below} theme first` };
  if (t.requires && !s.owned.includes(t.requires)) return { ok: false, reason: `own ${t.requires} first` };
  if (balanceOf(s) < t.price) return { ok: false, reason: "not enough coins" };
  s.spent += t.price;
  s.owned.push(t.name);
  return { ok: true, state: s };
}

const view = (s) => ({ balance: balanceOf(s), owned: s.owned, fortune: fortune(s.owned), repEarned: s.repEarned, repSpent: s.repSpent });

module.exports = async ({ params, context, logger }) => {
  const api = new DataApi(context);
  for (let attempt = 0; attempt < 3; attempt++) {
    const stored = await api.getProtectedItems(context.projectId, context.playerId, [STATE_KEY]);
    const item = stored.data && stored.data.results && stored.data.results[0];
    const prev = item ? item.value : null;
    const now = Date.now();

    let r = sync(prev, { earned: params.earned, spent: params.spent, legacyOwned: params.legacyOwned }, now);
    if (!r.ok) { logger.warning(`Wallet rejected ${context.playerId}: ${r.reason}`); return { ok: false, reason: r.reason }; }
    let state = r.state;
    let reason = null;
    if (params.op === "buy") {
      const b = buy(state, params.theme);
      if (b.ok) state = b.state; else reason = b.reason;
    } else if (params.op !== "sync") return { ok: false, reason: "unknown op" };

    try {
      await api.setProtectedItem(context.projectId, context.playerId, { key: STATE_KEY, value: state, writeLock: item ? item.writeLock : undefined });
    } catch (e) {
      if (attempt < 2) continue; // two requests raced: read again and redo
      throw e;
    }
    return Object.assign({ ok: reason === null, reason }, view(state));
  }
};

module.exports.params = {
  op: { type: "String", required: true },
  earned: { type: "Numeric", required: true },
  spent: { type: "Numeric", required: true },
  theme: { type: "String", required: false },
  legacyOwned: { type: "Any", required: false },
};

Object.assign(module.exports, { sync, buy, fortune, balanceOf, CATALOG, SERVER_MIN_PRICE, FORTUNE_BASE, DAY_EARN });
