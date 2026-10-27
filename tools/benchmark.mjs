import { performance } from "node:perf_hooks";
import fs from "node:fs";
import { generate, step, save, load } from "../examples/mosslight/src/world.js";
const stats = (values) => {
  values.sort((a, b) => a - b);
  return {
    min: values[0],
    p50: values[Math.floor(values.length * 0.5)],
    p95: values[Math.floor(values.length * 0.95)],
    max: values.at(-1),
  };
};
let times = [],
  saves = [],
  loads = [];
for (let i = 0; i < 100; i++) {
  let t = performance.now(),
    w = generate(i);
  times.push(performance.now() - t);
  t = performance.now();
  const raw = save(w);
  saves.push(performance.now() - t);
  t = performance.now();
  load(raw);
  loads.push(performance.now() - t);
}
const w = generate(42);
let t = performance.now();
for (let i = 0; i < 18000; i++) step(w, 1 / 30);
let ms = performance.now() - t;
const result = {
  environment: {
    node: process.version,
    platform: process.platform,
    arch: process.arch,
  },
  generationMs: stats(times),
  saveMs: stats(saves),
  loadMs: stats(loads),
  simulation: {
    steps: 18000,
    simSeconds: 600,
    wallMs: ms,
    stepsPerSecond: 18000 / (ms / 1000),
    agents: 8,
    meals: w.entities.reduce((s, e) => s + (e.meals || 0), 0),
  },
  saveBytes: Buffer.byteLength(save(w)),
  heapBytes: process.memoryUsage().heapUsed,
};
fs.writeFileSync(
  "docs/simulation-measurements.json",
  JSON.stringify(result, null, 2),
);
console.log(result);
