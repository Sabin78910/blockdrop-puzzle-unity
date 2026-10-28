// Run with: node --test CloudCode/
const test = require("node:test");
const assert = require("node:assert");
const fs = require("node:fs");
const path = require("node:path");
const Module = require("node:module");

const load = Module._load;
Module._load = (req, ...rest) => (req === "@unity-services/cloud-save-1.4" ? { DataApi: class {} } : load(req, ...rest));
const w = require("./Wallet.js");

const T0 = Date.UTC(2026, 9, 28, 12);
const DAY = 86400000;
const run = (prev, claim, t = T0) => w.sync(prev, claim, t);

test("catalog, prices, tiers and Fortune base match Meta.cs and Economy.cs", () => {
  const meta = fs.readFileSync(path.join(__dirname, "../Assets/Scripts/Core/Meta.cs"), "utf8");
  const economy = fs.readFileSync(path.join(__dirname, "../Assets/Scripts/Core/Economy.cs"), "utf8");
  const themes = [...meta.matchAll(/new Theme \{ Name = "([^"]+)",(?: Price = (\d+),)? Tier = ThemeTier\.(\w+),?(?: Requires = "([^"]*)",)?/g)]
    .map((m) => ({ name: m[1], price: Number(m[2] || 0), tier: m[3], requires: m[4] }));
  const sold = themes.filter((t) => t.tier !== "Exclusive" && t.price >= w.SERVER_MIN_PRICE);
  assert.deepStrictEqual(w.CATALOG.map((t) => [t.name, t.price, t.tier, t.requires]), sold.map((t) => [t.name, t.price, t.tier, t.requires]));
  assert.match(economy, new RegExp(`ServerMinPrice = ${w.SERVER_MIN_PRICE}, FortuneBase = ${w.FORTUNE_BASE}`));
});

test("first sync credits an old save's coins and honours legacy legendary themes once", () => {
  const r = run(null, { earned: 1125, spent: 0, legacyOwned: ["Aurora", "Singularity"] });
  assert.strictEqual(r.ok, true);
  assert.strictEqual(w.balanceOf(r.state), 1125);
  assert.deepStrictEqual(r.state.owned, ["Aurora"]); // Singularity was never sellable before
});

test("a huge claimed balance is clamped to the bucket", () => {
  const r = run(null, { earned: 999999999999, spent: 0 });
  assert.ok(w.balanceOf(r.state) <= w.DAY_EARN * 3 + 40000);
  const again = run(r.state, { earned: 999999999999, spent: 0 }, T0 + 1000);
  assert.strictEqual(again.credited, 0); // the counter was consumed, replay pays nothing
});

test("earning is rate limited over time and scales with Fortune", () => {
  let s = run(null, { earned: 0, spent: 0 }).state;
  s = run(s, { earned: 1e9, spent: 0 }, T0 + DAY).state;
  const low = w.balanceOf(s);
  let rich = Object.assign({}, s, { owned: ["Singularity"], earned: 0, spent: 0, repEarned: 0, bucket: 0, bucketMs: T0 });
  rich = run(rich, { earned: 1e12, spent: 0 }, T0 + DAY).state;
  assert.ok(w.balanceOf(rich) > low * 1000);
});

test("counters going backwards, negatives and junk are refused or ignored", () => {
  const s = run(null, { earned: 500, spent: 0 }).state;
  assert.strictEqual(run(s, { earned: -1, spent: 0 }).ok, false);
  assert.strictEqual(run(s, { earned: 1.5, spent: 0 }).ok, false);
  assert.strictEqual(run(s, { earned: "9", spent: 0 }).ok, false);
  assert.strictEqual(run(s, { earned: 10, spent: 0 }, T0 - 1).ok, false);
  assert.strictEqual(w.balanceOf(run(s, { earned: 10, spent: 0 }, T0 + 1).state), 500);
});

const rich = (owned = []) => ({ earned: 5e9, spent: 0, repEarned: 5e9, repSpent: 0, owned, bucket: 0, bucketMs: T0, started: true });

test("buying checks price, ladder and ownership", () => {
  assert.strictEqual(w.buy({ ...rich(), earned: 100 }, "Aurora").reason, "not enough coins");
  assert.strictEqual(w.buy(rich(), "Ember").reason, "own a Legendary theme first");
  assert.strictEqual(w.buy(rich(["Aurora"]), "Nebula").reason, "own a Mythic theme first");
  assert.strictEqual(w.buy(rich(["Pulsar", "Aether"]), "Singularity").reason, "own Genesis first");
  assert.strictEqual(w.buy(rich(["Aurora"]), "Aurora").reason, "already owned");
  assert.strictEqual(w.buy(rich(), "Jewel").reason, "not sold by the server");
  const ok = w.buy(rich(["Aurora"]), "Ember");
  assert.strictEqual(ok.ok, true);
  assert.strictEqual(w.balanceOf(ok.state), 5e9 - 15000);
});

test("the full ladder can be bought in order and ends at one billion", () => {
  let s = rich();
  for (const t of w.CATALOG) { const b = w.buy(s, t.name); assert.strictEqual(b.ok, true, `${t.name}: ${b.reason}`); s = b.state; }
  assert.strictEqual(s.owned.length, w.CATALOG.length);
  assert.strictEqual(w.fortune(s.owned), 1e9 / 9000);
});

test("spending is capped at the balance and counters only move forward", () => {
  const s = run(null, { earned: 100, spent: 0 }).state;
  const r = run(s, { earned: 100, spent: 5000 }, T0 + 1);
  assert.strictEqual(w.balanceOf(r.state), 0);
  assert.strictEqual(w.balanceOf(run(r.state, { earned: 100, spent: 0 }, T0 + 2).state), 0);
});
