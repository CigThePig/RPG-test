import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import {
  generate,
  path,
  passable,
  cell,
  distance,
  action,
  build,
  step,
  save,
  load,
} from "../src/world.js";
const player = (w) => w.entities.find((e) => e.kind === "player");
test("100 seeds: deterministic, valid spawns, reachable starter resources, variant worlds", () => {
  const signatures = new Set();
  for (const seed of [
    1,
    42,
    1337,
    ...Array.from({ length: 97 }, (_, i) => i + 100),
  ]) {
    const w = generate(seed);
    assert.deepEqual(w, generate(seed));
    signatures.add(w.terrain.join());
    assert.equal(new Set(w.entities.map((e) => e.id)).size, w.entities.length);
    for (const e of w.entities)
      assert.ok(passable(w, ...cell(e), e.flies), `${seed}: ${e.id}`);
    for (const kind of ["bush", "wood"])
      assert.ok(
        w.entities.some(
          (e) => e.kind === kind && path(w, player(w), e).length > 0,
        ),
      );
  }
  assert.ok(signatures.size > 90);
});
test("failed actions are atomic; depleted resources cannot be consumed twice", () => {
  const w = generate(),
    p = player(w),
    b = w.entities.find((e) => e.kind === "bush");
  b.food = 1;
  let before = save(w);
  assert.equal(action(w, p.id, b.id, "harvest").ok, false);
  assert.equal(save(w), before);
  p.x = b.x;
  p.y = b.y;
  assert.ok(action(w, p.id, b.id, "harvest").ok);
  before = save(w);
  assert.equal(action(w, p.id, b.id, "harvest").ok, false);
  assert.equal(save(w), before);
  assert.equal(p.berries, 1);
});
test("planting consumes a seed berry and becomes an autonomous food source", () => {
  const w = generate(),
    p = player(w);
  w.entities = [p];
  p.x = 8 * 48;
  p.y = 2 * 32;
  p.berries = 1;
  assert.ok(build(w, "plant").ok);
  const b = w.entities.at(-1);
  assert.equal(p.berries, 0);
  assert.equal(b.food, 0);
  for (let i = 0; i < 601; i++) step(w, 1 / 30);
  assert.ok(b.food > 0);
});
test("feeding station connects player resources to NPC behaviour and meal events", () => {
  const w = generate(),
    p = player(w);
  w.entities = w.entities.filter((e) => ["player", "agent"].includes(e.kind));
  p.x = 300;
  p.y = 300;
  p.wood = 3;
  p.berries = 1;
  assert.ok(build(w, "tray").ok);
  const tray = w.entities.at(-1);
  assert.ok(action(w, p.id, tray.id, "feed").ok);
  const a = w.entities.find((e) => e.kind === "agent");
  a.x = tray.x + 40;
  a.y = tray.y;
  a.hunger = 0.9;
  for (let i = 0; i < 120; i++) step(w, 1 / 30);
  assert.equal(tray.food, 0);
  assert.ok(w.events.some((e) => e.type === "meal" && e.target === tray.id));
});
test("agents replan when a target is depleted or removed; no food is a valid waiting state", () => {
  const w = generate();
  const a = w.entities.find((e) => e.kind === "agent");
  a.hunger = 0.9;
  step(w, 0.01);
  assert.ok(a.target);
  const old = a.target;
  w.entities = w.entities.filter((e) => e.id !== old);
  step(w, 0.01);
  assert.notEqual(a.target, old);
  w.entities = w.entities.filter((e) => e.kind !== "bush" && e.kind !== "tray");
  step(w, 0.01);
  assert.equal(a.target, null);
  assert.equal(a.goal, "Waiting for reachable food");
});
test("semantic wings permit water routes that ground creatures cannot take", () => {
  const w = generate();
  w.terrain.fill("grass");
  for (let y = 0; y < 26; y++) w.terrain[y * 32 + 15] = "water";
  const a = { x: 10 * 48, y: 10 * 32 },
    b = { x: 20 * 48, y: 10 * 32 };
  assert.equal(path(w, a, b, false).length, 0);
  assert.ok(path(w, a, b, true).length > 0);
  const agents = w.entities.filter((e) => e.kind === "agent");
  assert.ok(agents.some((e) => e.flies));
  assert.ok(agents.some((e) => !e.flies));
});
test("save/load preserves authoritative changes, identities, paths and deterministic continuation", () => {
  const w = generate(1337);
  for (let i = 0; i < 300; i++) step(w, 1 / 30);
  player(w).berries = 5;
  const raw = save(w),
    restored = load(raw);
  assert.deepEqual(restored, w);
  for (let i = 0; i < 100; i++) {
    step(w, 1 / 30);
    step(restored, 1 / 30);
  }
  assert.deepEqual(restored, w);
  assert.throws(() => load("{}"));
  assert.throws(() => load(raw.replace('"version":1', '"version":99')));
});
test("30 seeds run ten minutes: bounded resources, valid movement, observed independent meals", () => {
  for (let seed = 0; seed < 30; seed++) {
    const w = generate(seed);
    for (let i = 0; i < 6000; i++) step(w, 0.1);
    for (const e of w.entities) {
      assert.ok(passable(w, ...cell(e), e.flies));
      if (e.kind === "bush") assert.ok(e.food >= 0 && e.food <= 4);
      if (e.kind === "agent")
        assert.ok(Number.isFinite(e.hunger) && e.hunger >= 0 && e.hunger <= 1);
    }
    assert.ok(w.entities.some((e) => e.meals > 0));
  }
});
test("PPC export metadata: pages exist, frames fit, pivots valid, all eight directions", () => {
  const root = "public/art/",
    manifest = JSON.parse(fs.readFileSync(root + "manifest.json"));
  for (const [key, v] of Object.entries(manifest)) {
    if (!v.family) {
      assert.ok(fs.existsSync(root + key + ".png"));
      continue;
    }
    const m = JSON.parse(fs.readFileSync(root + key + ".json"));
    assert.equal(m.format, "ppc.spritesheet");
    assert.equal(m.clips.length, 16);
    assert.ok(m.originX >= 0 && m.originX < m.frameWidth);
    assert.ok(m.originY >= 0 && m.originY < m.frameHeight);
    for (const c of m.clips) {
      assert.ok(c.frames.length > 1);
      for (const f of c.frames) {
        let page = m.pages[f.page];
        assert.ok(fs.existsSync(root + page.file));
        assert.ok(
          f.x + m.frameWidth <= page.width &&
            f.y + m.frameHeight <= page.height,
        );
      }
    }
  }
  assert.notEqual(
    manifest["quadruped-17"].anatomyKey,
    manifest["quadruped-1337"].anatomyKey,
  );
  assert.ok(manifest["winged-91"].wings > 0);
});
test("corrupt saves reject invalid inventory, sprite references, routes and simulation values", () => {
  const raw = save(generate(42));
  for (const corrupt of [
    (v) => (v.state.entities[0].berries = -1),
    (v) => (v.state.entities[0].asset = "missing"),
    (v) => (v.state.entities.find((e) => e.kind === "agent").hunger = 99),
    (v) =>
      (v.state.entities.find((e) => e.kind === "agent").route = [
        { x: -100, y: 0 },
      ]),
    (v) => (v.state.time = -1),
  ]) {
    const v = JSON.parse(raw);
    corrupt(v);
    assert.throws(() => load(JSON.stringify(v)));
  }
});
