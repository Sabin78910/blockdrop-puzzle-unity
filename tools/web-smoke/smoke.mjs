// Usage: node smoke.mjs <webgl build dir> <screenshot dir>
// Serves the build on 127.0.0.1 (a secure context, so the service worker registers) and checks, on a desktop
// and a phone screen, that the game starts with no script errors or Content-Security-Policy violations.
// On desktop it then goes offline and reloads, to prove the game is playable without a network.
import { createServer } from "node:http";
import { readFile, mkdir } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { chromium, devices } from "playwright";

const [root, shots = "smoke"] = process.argv.slice(2);
if (!root) { console.error("usage: node smoke.mjs <build dir> [screenshot dir]"); process.exit(2); }
await mkdir(shots, { recursive: true });

const types = {
  ".html": "text/html", ".js": "text/javascript", ".css": "text/css", ".json": "application/json",
  ".webmanifest": "application/manifest+json", ".png": "image/png", ".wasm": "application/wasm",
  ".data": "application/octet-stream", ".unityweb": "application/octet-stream", ".gz": "application/octet-stream",
};
const server = createServer(async (req, res) => {
  let path = decodeURIComponent(new URL(req.url, "http://x").pathname);
  if (path.endsWith("/")) path += "index.html";
  const file = normalize(join(root, path));
  if (!file.startsWith(normalize(root))) { res.writeHead(403).end(); return; }
  try {
    const body = await readFile(file);
    res.writeHead(200, { "content-type": types[extname(file)] || "application/octet-stream" }).end(body);
  } catch { res.writeHead(404).end("not found"); }
});
await new Promise(r => server.listen(0, "127.0.0.1", r));
const url = `http://127.0.0.1:${server.address().port}/`;

const browser = await chromium.launch({ args: ["--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--autoplay-policy=no-user-gesture-required"] });
const failures = [];

async function run(name, options, offline) {
  const context = await browser.newContext(options);
  await context.addInitScript(() => {
    window.__csp = [];
    document.addEventListener("securitypolicyviolation", e => window.__csp.push(`${e.violatedDirective} ${e.blockedURI}`));
  });
  const page = await context.newPage();
  const errors = [];
  page.on("pageerror", e => errors.push(`pageerror: ${e.message}`));
  page.on("console", m => { if (m.type() === "error") console.log(`[${name}] console error: ${m.text()}`); });

  async function boot(label) {
    const t0 = Date.now();
    await page.goto(url, { waitUntil: "domcontentloaded" });
    await page.waitForFunction(() => window.__blockDrop && (window.__blockDrop.ready || window.__blockDrop.error), null, { timeout: 300_000 });
    const st = await page.evaluate(() => ({ ...window.__blockDrop, csp: window.__csp }));
    console.log(`[${name}] ${label}: ready=${st.ready} in ${((Date.now() - t0) / 1000).toFixed(1)}s`);
    if (!st.ready) failures.push(`${name} ${label}: ${st.error}`);
    if (st.csp.length) failures.push(`${name} ${label}: CSP violations: ${st.csp.join(", ")}`);
    return st.ready;
  }

  if (await boot("online")) {
    await page.waitForTimeout(4000);
    await page.screenshot({ path: join(shots, `${name}-menu.png`) });
    const box = await page.locator("#game").boundingBox();
    // Tap around the menu so input paths run; the game must survive it.
    for (const [fx, fy] of [[0.5, 0.55], [0.5, 0.5], [0.5, 0.75]]) {
      const x = box.x + box.width * fx, y = box.y + box.height * fy;
      if (options.hasTouch) await page.touchscreen.tap(x, y); else await page.mouse.click(x, y);
      await page.waitForTimeout(1500);
    }
    await page.screenshot({ path: join(shots, `${name}-after-taps.png`) });

    if (offline) {
      await page.waitForFunction(async () => {
        const reg = await navigator.serviceWorker.getRegistration();
        if (!reg || !reg.active) return false;
        const keys = await caches.keys();
        return keys.some(k => k.startsWith("blockdrop-") && !k.endsWith("__BUILD_ID__"));
      }, null, { timeout: 120_000, polling: 1000 });
      await context.setOffline(true);
      if (await boot("offline reload")) await page.screenshot({ path: join(shots, `${name}-offline.png`) });
      await context.setOffline(false);
    }
  }
  if (errors.length) failures.push(`${name}: ${errors.join(" | ")}`);
  await context.close();
}

try {
  await run("desktop", { viewport: { width: 1280, height: 800 } }, true);
  await run("phone", { ...devices["Pixel 7"] }, false);
} finally {
  await browser.close();
  server.close();
}

if (failures.length) { console.error("Web smoke test failed:\n- " + failures.join("\n- ")); process.exit(1); }
console.log("Web smoke test passed.");
