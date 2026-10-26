import { chromium } from "playwright";
import assert from "node:assert/strict";
import fs from "node:fs";
import { preview } from "vite";
const server = process.env.GAME_URL
  ? null
  : await preview({
      preview: { host: "127.0.0.1", port: 5173, strictPort: true },
    });
let browser;
try {
  fs.mkdirSync("docs/screenshots", { recursive: true });
  browser = await chromium.launch({
    headless: true,
    args: ["--no-sandbox"],
  });
  const page = await browser.newPage({
    viewport: { width: 1440, height: 1000 },
  });
  const errors = [];
  page.on("pageerror", (e) => errors.push(e.message));
  page.on("response", (r) => {
    if (r.status() >= 400 && new URL(r.url()).host === "127.0.0.1:5173")
      errors.push(`${r.status()} ${r.url()}`);
  });
  await page.goto(process.env.GAME_URL ?? "http://127.0.0.1:5173");
  await page.waitForFunction(() => window.game?.world);
  await page.screenshot({ path: "docs/screenshots/arrival.png" });
  await page.locator("#begin").click();
  const start = await page.evaluate(
    () => window.game.world.entities.find((e) => e.kind === "player").x,
  );
  await page.keyboard.down("d");
  await page.waitForTimeout(300);
  await page.keyboard.up("d");
  assert.ok(
    (await page.evaluate(
      () => window.game.world.entities.find((e) => e.kind === "player").x,
    )) > start,
  );
  // Approach the real starter patch through the public input, then harvest with E.
  await page.keyboard.down("d");
  await page.waitForTimeout(400);
  await page.keyboard.up("d");
  await page.keyboard.press("e");
  assert.match(await page.locator("#pouch").textContent(), /[1-9]\d* berries/);
  await page.locator("#save").click();
  const saved = await page.evaluate(() => localStorage.getItem("mosslight-v1"));
  assert.ok(saved);
  await page.locator("#seed").fill("1337");
  await page.locator("#seedform button").click();
  assert.equal(await page.evaluate(() => window.game.world.seed), 1337);
  await page.locator("#load").click();
  assert.equal(await page.evaluate(() => window.game.world.seed), 42);
  await page.waitForTimeout(5000);
  await page.screenshot({ path: "docs/screenshots/gameplay.png" });
  await page.locator("#journal").click();
  await page.screenshot({ path: "docs/screenshots/field-notes.png" });
  assert.ok((await page.locator("#agents p").count()) === 8);
  await page.locator("#close").click();
  await page.waitForTimeout(5000);
  const performanceData = await page.evaluate(() => {
    const f = window.game.metrics.frames.slice(10).sort((a, b) => a - b);
    return {
      samples: f.length,
      frameMs: {
        p50: f[Math.floor(f.length * 0.5)],
        p95: f[Math.floor(f.length * 0.95)],
        p99: f[Math.floor(f.length * 0.99)],
      },
      generationMs: window.game.metrics.generationMs,
      terrainCacheMs: window.game.metrics.terrainCacheMs,
      meanFrameMs: f.reduce((s, v) => s + v, 0) / f.length,
      longFrameFraction: f.filter((v) => v > 25).length / f.length,
      steps: window.game.metrics.steps,
      simSeconds: window.game.world.time,
      heapBytes: performance.memory?.usedJSHeapSize,
      agents: window.game.world.entities.filter((e) => e.kind === "agent")
        .length,
      meals: window.game.world.entities.reduce((n, e) => n + (e.meals || 0), 0),
    };
  });
  await page.locator("#pause").click();
  const pausedTime = await page.evaluate(() => window.game.world.time);
  await page.waitForTimeout(150);
  assert.equal(await page.evaluate(() => window.game.world.time), pausedTime);
  await page.locator("#pause").click();
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: "docs/screenshots/mobile.png" });
  assert.equal(
    await page.evaluate(() => document.documentElement.scrollWidth),
    390,
  );
  const mobileStart = await page.evaluate(() => ({
    x: window.game.world.entities[0].x,
    y: window.game.world.entities[0].y,
  }));
  const bounds = await page.locator("#game").boundingBox();
  await page.mouse.click(
    bounds.x + bounds.width / 2 + 55,
    bounds.y + bounds.height / 2,
  );
  await page.waitForTimeout(1000);
  assert.ok(
    await page.evaluate(
      (p) =>
        Math.hypot(
          window.game.world.entities[0].x - p.x,
          window.game.world.entities[0].y - p.y,
        ) > 10,
      mobileStart,
    ),
  );
  await page.locator("#plant").click();
  await page.locator("#journal").click();
  assert.ok(await page.locator("#notes").isVisible());
  assert.deepEqual(errors, []);
  // Network-delayed startup must not allow rendering before assets are decoded.
  const slowPage = await browser.newPage();
  slowPage.on("pageerror", (e) => errors.push(e.message));
  await slowPage.route("**/art/manifest.json", async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 800));
    await route.continue();
  });
  await slowPage.goto(process.env.GAME_URL ?? "http://127.0.0.1:5173");
  assert.ok(await slowPage.locator("#begin").isDisabled());
  await slowPage.locator("#seed").fill("1337");
  await slowPage.locator("#seedform button").click();
  await slowPage.waitForFunction(() => window.game?.world);
  assert.equal(await slowPage.evaluate(() => window.game.world.seed), 1337);
  assert.ok(await slowPage.locator("#begin").isEnabled());
  await slowPage.evaluate(() => localStorage.setItem("mosslight-v1", "{}"));
  await slowPage.locator("#load").click();
  assert.match(await slowPage.locator("#hint").textContent(), /invalid save/i);
  assert.deepEqual(errors, []);
  await slowPage.close();
  fs.writeFileSync(
    "docs/browser-measurements.json",
    JSON.stringify(performanceData, null, 2),
  );
  console.log(performanceData);
} finally {
  await browser?.close();
  await new Promise((resolve) =>
    server ? server.httpServer.close(resolve) : resolve(),
  );
}
