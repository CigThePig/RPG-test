import "./style.css";
import {
  generate,
  step,
  move,
  action,
  build,
  path,
  distance,
  cell,
  surface,
  save,
  load,
  W,
  H,
  TW,
  TH,
} from "./world.js";
const $ = (id) => document.getElementById(id),
  canvas = $("game"),
  ctx = canvas.getContext("2d");
let world = generate(42),
  ready = false,
  paused = false,
  started = false,
  route = [],
  keys = new Set(),
  selected = null,
  assets = {},
  metadata = {},
  manifest,
  ground,
  camera = { x: 0, y: 0, zoom: 1 },
  last = 0,
  accumulator = 0,
  uiClock = 0;
const metrics = { frames: [], steps: 0, generationMs: 0 };
let t = performance.now();
generate(42);
metrics.generationMs = performance.now() - t;
const player = () => world.entities.find((e) => e.kind === "player");
function hint(s) {
  $("hint").textContent = s;
}
function setWorld(w) {
  world = w;
  route = [];
  selected = null;
  keys.clear();
  $("seed").value = w.seed;
  if (ready) makeGround();
  updateUI();
}
async function image(file) {
  const im = new Image();
  im.src = import.meta.env.BASE_URL + "art/" + file;
  await im.decode();
  return im;
}
async function init() {
  manifest = await fetch(import.meta.env.BASE_URL + "art/manifest.json").then(
    (r) => {
      if (!r.ok) throw Error("Missing art manifest");
      return r.json();
    },
  );
  await Promise.all(
    Object.keys(manifest).map(async (key) => {
      if (manifest[key].family) {
        const m = await fetch(
          import.meta.env.BASE_URL + `art/${key}.json`,
        ).then((r) => r.json());
        metadata[key] = m;
        for (const p of m.pages) assets[p.file] = await image(p.file);
      } else assets[key] = await image(key + ".png");
    }),
  );
  ready = true;
  makeGround();
  $("begin").disabled = false;
  hint("Click Explore to begin.");
  requestAnimationFrame(frame);
  window.game = {
    get world() {
      return world;
    },
    metrics,
    action,
    build,
    step,
    setWorld,
    save,
    load,
  };
}
function makeGround() {
  ground = document.createElement("canvas");
  ground.width = W * TW;
  ground.height = H * TH;
  const g = ground.getContext("2d");
  const start = performance.now();
  const textures = {};
  const bases = {
    grass: [88, 114, 71],
    sand: [186, 168, 123],
    dirt: [140, 118, 89],
    water: [55, 101, 116],
  };
  for (const kind of Object.keys(bases)) {
    const c = document.createElement("canvas");
    c.width = c.height = 64;
    const cc = c.getContext("2d");
    cc.drawImage(assets[kind], 0, 0);
    textures[kind] = cc.getImageData(0, 0, 64, 64).data;
  }
  const data = g.createImageData(ground.width, ground.height);
  for (let y = 0; y < ground.height; y++)
    for (let x = 0; x < ground.width; x++) {
      const kind = surface(world, x / TW, y / TH),
        base = bases[kind],
        tex = textures[kind];
      const j =
          ((Math.floor((y / TH) * 64) % 64) * 64 +
            (Math.floor((x / TW) * 64) % 64)) *
          4,
        i = (y * ground.width + x) * 4,
        alpha = kind === "water" ? 0.55 : 0.28;
      for (let k = 0; k < 3; k++)
        data.data[i + k] = Math.round(
          base[k] * (1 - alpha) + tex[j + k] * alpha,
        );
      data.data[i + 3] = 255;
    }
  g.putImageData(data, 0, 0);
  metrics.terrainCacheMs = performance.now() - start;
}
function resize() {
  const d = Math.min(devicePixelRatio, 2),
    r = canvas.getBoundingClientRect();
  if (
    canvas.width !== Math.round(r.width * d) ||
    canvas.height !== Math.round(r.height * d)
  ) {
    canvas.width = Math.round(r.width * d);
    canvas.height = Math.round(r.height * d);
  }
  camera.zoom = r.width < 700 ? 1 : 1.35;
  const viewW = r.width / camera.zoom,
    viewH = r.height / camera.zoom;
  camera.x = Math.max(0, Math.min(W * TW - viewW, player().x - viewW / 2));
  camera.y = Math.max(0, Math.min(H * TH - viewH, player().y - viewH / 2));
  ctx.setTransform(
    d * camera.zoom,
    0,
    0,
    d * camera.zoom,
    -camera.x * d * camera.zoom,
    -camera.y * d * camera.zoom,
  );
  ctx.imageSmoothingEnabled = false;
}
function creature(e) {
  const m = metadata[e.asset],
    state = e.moving ? "walk" : "idle";
  const clip = m.clips.find(
    (c) => c.state === state && c.direction === (e.dir ?? 6),
  );
  const f = clip.frames[Math.floor(world.time * clip.fps) % clip.frames.length];
  ctx.drawImage(
    assets[m.pages[f.page].file],
    f.x,
    f.y,
    m.frameWidth,
    m.frameHeight,
    Math.round(e.x - m.originX),
    Math.round(e.y - m.originY),
    m.frameWidth,
    m.frameHeight,
  );
  if (e.kind === "player") {
    ctx.strokeStyle = "#f1d58a";
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.ellipse(e.x, e.y + 2, 14, 5, 0, 0, Math.PI * 2);
    ctx.stroke();
  }
}
function prop(e) {
  if (e.kind === "wood") {
    if (e.amount === 0) return;
    ctx.fillStyle = "#392f25";
    ctx.fillRect(e.x - 17, e.y - 6, 34, 10);
    ctx.fillStyle = "#bd9460";
    ctx.fillRect(e.x - 16, e.y - 8, 31, 5);
    ctx.fillStyle = "#dfba7a";
    ctx.fillRect(e.x - 16, e.y - 7, 4, 10);
    return;
  }
  if (e.kind === "tray") {
    ctx.fillStyle = "#2d3728";
    ctx.fillRect(e.x - 18, e.y - 9, 36, 16);
    ctx.fillStyle = "#80684b";
    ctx.fillRect(e.x - 20, e.y - 14, 40, 13);
    ctx.fillStyle = "#be9c68";
    ctx.fillRect(e.x - 18, e.y - 15, 36, 4);
    ctx.fillStyle = "#b74d60";
    for (let i = 0; i < Math.min(e.food, 8); i++)
      ctx.fillRect(
        e.x - 13 + (i % 4) * 7,
        e.y - 10 + Math.floor(i / 4) * 4,
        4,
        3,
      );
    return;
  }
  const a = manifest[e.asset];
  ctx.save();
  if (e.kind === "bush" && e.food === 0) ctx.globalAlpha = 0.55;
  ctx.drawImage(assets[e.asset], Math.round(e.x - a.x), Math.round(e.y - a.y));
  ctx.restore();
  if (e.kind === "bush" && e.food > 0) {
    ctx.fillStyle = "#ef9d9a";
    for (let i = 0; i < e.food; i++)
      ctx.fillRect(e.x - 11 + i * 6, e.y - 16 - (i % 2) * 5, 3, 3);
  }
}
function draw() {
  resize();
  ctx.fillStyle = "#254538";
  ctx.fillRect(
    camera.x,
    camera.y,
    canvas.clientWidth / camera.zoom,
    canvas.clientHeight / camera.zoom,
  );
  ctx.drawImage(ground, 0, 0);
  const list = [...world.entities].sort((a, b) => a.y - b.y);
  for (const e of list) {
    if (
      e.x < camera.x - 140 ||
      e.x > camera.x + canvas.clientWidth / camera.zoom + 140 ||
      e.y < camera.y - 50 ||
      e.y > camera.y + canvas.clientHeight / camera.zoom + 180
    )
      continue;
    if (e.kind === "player" || e.kind === "agent") creature(e);
    else prop(e);
  }
  const nearest = near();
  if (nearest && started) {
    ctx.strokeStyle = "#edd696";
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.ellipse(nearest.x, nearest.y, 23, 9, 0, 0, Math.PI * 2);
    ctx.stroke();
  }
  if (route.length) {
    const n = route.at(-1);
    ctx.strokeStyle = "#e9d38a88";
    ctx.strokeRect(n.x - 5, n.y - 3, 10, 6);
  }
}
function near() {
  return world.entities
    .filter(
      (e) =>
        ["agent", "bush", "tray", "wood"].includes(e.kind) &&
        distance(player(), e) < 85 &&
        (e.kind !== "wood" || e.amount > 0),
    )
    .sort((a, b) => distance(player(), a) - distance(player(), b))[0];
}
function interact() {
  const target =
    selected && distance(player(), selected) < 85 ? selected : near();
  if (!target) {
    hint("Approach berries, fallen wood, a feeder, or a forager.");
    return;
  }
  const verb = {
    bush: "harvest",
    wood: "gather",
    tray: "feed",
    agent: "inspect",
  }[target.kind];
  hint(action(world, player().id, target.id, verb).message);
  selected = target;
  updateUI();
}
function updateUI() {
  const p = player();
  $("pouch").textContent = `${p.berries} berries · ${p.wood} wood`;
  $("clock").textContent =
    `${Math.floor(world.time / 60)}:${String(Math.floor(world.time % 60)).padStart(2, "0")}`;
  if (selected) {
    $("selection").textContent =
      selected.kind === "agent"
        ? `${selected.name} · ${selected.goal}\n${selected.flies ? "Winged • can cross water" : "Ground-bound • follows land routes"}`
        : selected.kind === "bush"
          ? `Berry patch #${selected.id} · ${selected.food}/4 ripe\nRegrows one berry every 20 seconds.`
          : selected.kind === "tray"
            ? `Feeding station · ${selected.food} berries. E adds one.`
            : "Fallen wood · E to gather";
  } else $("selection").textContent = "";
  if (!$("notes").hidden) {
    $("stats").textContent =
      `Seed ${world.seed} · ${world.entities.filter((e) => e.kind === "agent").length} foragers\n${world.harvested} berries gathered · ${world.planted} patches planted`;
    $("agents").replaceChildren(
      ...world.entities
        .filter((e) => e.kind === "agent")
        .map((e) => {
          const p = document.createElement("p");
          const b = document.createElement("b");
          b.textContent = e.name;
          p.append(
            b,
            ` · hunger ${Math.round(e.hunger * 100)}%\n${e.goal}. Meals ${e.meals}`,
          );
          return p;
        }),
    );
    $("events").replaceChildren(
      ...world.events
        .slice()
        .reverse()
        .map((e) => {
          let p = document.createElement("p");
          p.textContent = `${e.time.toFixed(1)}s · ${e.text}`;
          return p;
        }),
    );
  }
}
function frame(now) {
  const elapsed = last ? Math.min((now - last) / 1000, 0.15) : 0;
  if (last) {
    metrics.frames.push(now - last);
    if (metrics.frames.length > 3600) metrics.frames.shift();
  }
  last = now;
  if (started && !paused && !document.hidden) {
    accumulator += elapsed;
    while (accumulator >= 1 / 30) {
      step(world, 1 / 30);
      metrics.steps++;
      const p = player();
      let dx =
          (keys.has("d") || keys.has("arrowright") ? 1 : 0) -
          (keys.has("a") || keys.has("arrowleft") ? 1 : 0),
        dy =
          (keys.has("s") || keys.has("arrowdown") ? 1 : 0) -
          (keys.has("w") || keys.has("arrowup") ? 1 : 0);
      p.moving = false;
      if (dx || dy) {
        route = [];
        let len = Math.hypot(dx, dy);
        p.moving = move(
          world,
          p,
          ((dx / len) * 90) / 30,
          ((dy / len) * 90) / 30,
        );
      } else if (route.length) {
        const n = route[0],
          dist = distance(p, n);
        if (dist < 3) route.shift();
        else
          p.moving = move(
            world,
            p,
            ((n.x - p.x) / dist) * Math.min(dist, 3),
            ((n.y - p.y) / dist) * Math.min(dist, 3),
          );
      }
      accumulator -= 1 / 30;
    }
  }
  uiClock += elapsed;
  if (uiClock > 0.2) {
    updateUI();
    uiClock = 0;
  }
  draw();
  requestAnimationFrame(frame);
}
$("begin").onclick = () => {
  started = true;
  document.querySelector(".intro").hidden = true;
  hint(
    "Try the berry patch to the east. E picks a berry; 1 plants a new patch.",
  );
  canvas.focus();
};
$("interact").onclick = interact;
$("plant").onclick = () => {
  hint(build(world, "plant").message);
  updateUI();
};
$("build").onclick = () => {
  hint(build(world, "tray").message);
  updateUI();
};
$("pause").onclick = () => {
  paused = !paused;
  $("pause").textContent = paused ? "Resume" : "Pause";
};
$("journal").onclick = () => {
  $("notes").hidden = !$("notes").hidden;
  updateUI();
};
$("close").onclick = () => {
  $("notes").hidden = true;
};
$("save").onclick = () => {
  try {
    localStorage.setItem("mosslight-v1", save(world));
    hint("World saved on this device.");
  } catch {
    hint("Could not save: browser storage is unavailable or full.");
  }
};
$("load").onclick = () => {
  try {
    const raw = localStorage.getItem("mosslight-v1");
    if (!raw) throw Error("No save on this device.");
    setWorld(load(raw));
    hint("Saved world restored.");
  } catch (e) {
    hint(e.message);
  }
};
$("seedform").onsubmit = (e) => {
  e.preventDefault();
  const seed = Number($("seed").value);
  if (!Number.isInteger(seed) || seed < 0 || seed > 4294967295) {
    hint("Enter a whole seed from 0 to 4294967295.");
    return;
  }
  setWorld(generate(seed));
  hint("A new Reach. Your previous save remains available.");
};
window.addEventListener("keydown", (e) => {
  if (e.target instanceof HTMLInputElement) return;
  const k = e.key.toLowerCase();
  if (["arrowup", "arrowdown", "arrowleft", "arrowright", " "].includes(k))
    e.preventDefault();
  keys.add(k);
  if (e.repeat || !started) return;
  if (k === "e") interact();
  if (k === "1") $("plant").click();
  if (k === "2") $("build").click();
  if (k === "escape") $("pause").click();
});
window.addEventListener("keyup", (e) => keys.delete(e.key.toLowerCase()));
window.addEventListener("blur", () => keys.clear());
document.addEventListener("visibilitychange", () => {
  keys.clear();
  last = 0;
  accumulator = 0;
});
canvas.addEventListener("pointerdown", (e) => {
  if (!started) return;
  canvas.focus();
  const r = canvas.getBoundingClientRect(),
    pos = {
      x: (e.clientX - r.left) / camera.zoom + camera.x,
      y: (e.clientY - r.top) / camera.zoom + camera.y,
    };
  selected =
    world.entities
      .filter(
        (e) =>
          e.kind !== "scenery" && e.kind !== "player" && distance(pos, e) < 35,
      )
      .sort((a, b) => distance(pos, a) - distance(pos, b))[0] ?? null;
  route = path(world, player(), selected ?? pos);
  if (!route.length && distance(player(), pos) > 50)
    hint("No land route there. Winged foragers can cross the water.");
  updateUI();
});
init().catch((e) => {
  hint(
    `Unable to load artwork: ${e.message}. Reload or check the asset build.`,
  );
  console.error(e);
});
