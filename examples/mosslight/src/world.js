import catalog from "./anatomy.json" with { type: "json" };
export const VERSION = 1,
  W = 32,
  H = 26,
  TW = 48,
  TH = 32;
export function rng(seed, label = "world") {
  let a =
    (seed ^
      Array.from(label).reduce(
        (v, c) => (Math.imul(v, 31) + c.charCodeAt(0)) | 0,
        0,
      )) >>>
    0;
  return () => {
    a += 0x6d2b79f5;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
export const distance = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
export const cell = (e) => [Math.floor(e.x / TW), Math.floor(e.y / TH)];
export function tile(w, x, y) {
  return x < 0 || y < 0 || x >= W || y >= H ? "void" : w.terrain[y * W + x];
}
export function passable(w, x, y, flies = false) {
  let t = tile(w, x, y);
  return t !== "void" && (flies || t !== "water");
}
// Continuous geography shared by the visual ground and the navigation grid sample.
export function surface(w, x, y) {
  const { phase, width } = w.geography;
  const river = 22 + Math.sin(y * 0.28 + phase) * 2;
  return Math.abs(x - river) < width
    ? "water"
    : Math.abs(x - river) < width + 1
      ? "sand"
      : Math.abs(y - 13 - Math.sin(x * 0.24 + phase) * 0.9) < 1.2 ||
          Math.abs(x - 8 - Math.sin(y * 0.23) * 0.9) < 0.8
        ? "dirt"
        : "grass";
}
export function generate(seed = 42) {
  seed = Number(seed) >>> 0;
  const r = rng(seed, "geography"),
    p = rng(seed, "population"),
    d = rng(seed, "decoration");
  const phase = r() * 6.28,
    width = 1.2 + r() * 1.7;
  const geography = { phase, width };
  const terrain = [];
  for (let y = 0; y < H; y++)
    for (let x = 0; x < W; x++)
      terrain.push(surface({ geography }, x + 0.5, y + 0.5));
  const w = {
    version: VERSION,
    seed,
    time: 0,
    terrain,
    geography,
    entities: [],
    events: [],
    nextId: 1,
    planted: 0,
    harvested: 0,
  };
  const add = (kind, x, y, extra = {}) => {
    let e = { id: w.nextId++, kind, x: x * TW + 24, y: y * TH + 16, ...extra };
    w.entities.push(e);
    return e;
  };
  add("player", 8, 13, {
    name: "Wayfarer",
    asset: "biped-42",
    berries: 0,
    wood: 0,
    dir: 6,
  });
  for (let i = 0; i < 23; i++) {
    let x = 2 + Math.floor(p() * 27),
      y = 2 + Math.floor(p() * 22);
    if (
      passable(w, x, y) &&
      !w.entities.some(
        (e) => distance(e, { x: x * TW + 24, y: y * TH + 16 }) < 60,
      )
    )
      add("bush", x, y, {
        food: 2 + Math.floor(p() * 3),
        regrow: 0,
        asset: "bush" + (1 + (i % 5)),
      });
  }
  // Guaranteed reachable starter resources, away from random placements.
  for (const [x, y, kind] of [
    [10, 13, "bush"],
    [6, 14, "wood"],
    [11, 16, "wood"],
    [5, 9, "wood"],
  ]) {
    w.entities = w.entities.filter(
      (e) =>
        distance(e, { x: x * TW + 24, y: y * TH + 16 }) > 50 ||
        e.kind === "player",
    );
    add(kind, x, y, { food: 4, regrow: 0, amount: 3, asset: "bush1" });
  }
  for (let i = 0; i < 8; i++) {
    let x = 4 + Math.floor(p() * 13),
      y = 4 + Math.floor(p() * 17);
    while (
      w.entities.some(
        (e) => distance(e, { x: x * TW + 24, y: y * TH + 16 }) < 36,
      )
    ) {
      x = 4 + ((x - 3) % 13);
      if (x === 4) y = 4 + ((y - 3) % 17);
    }
    let asset = ["quadruped-17", "quadruped-1337", "winged-91", "arthropod-61"][
      i % 4
    ];
    add("agent", x, y, {
      name: [
        "Fern",
        "Bramble",
        "Saffron",
        "Thimble",
        "Mallow",
        "Reed",
        "Pip",
        "Juniper",
      ][i],
      asset,
      flies: catalog[asset].flies,
      speed: catalog[asset].speed,
      hunger: 0.25 + p() * 0.4,
      goal: "Looking for food",
      target: null,
      dir: 6,
      meals: 0,
    });
  }
  for (let i = 0; i < 100; i++) {
    let x = Math.floor(d() * W),
      y = Math.floor(d() * H);
    const pos = { x: x * TW + 24, y: y * TH + 16 };
    if (
      tile(w, x, y) === "grass" &&
      !w.entities.some((e) => distance(e, pos) < 65)
    )
      add("scenery", x, y, {
        asset: (d() < 0.8 ? "tree" : "rock") + (1 + (i % 5)),
      });
  }
  record(
    w,
    "arrival",
    "You reached Mosslight. Food draws creatures; depleted patches send them elsewhere.",
  );
  return w;
}
export function record(w, type, text, actor = null, target = null) {
  w.events.push({
    time: Math.round(w.time * 10) / 10,
    type,
    text,
    actor,
    target,
  });
  if (w.events.length > 100) w.events.shift();
}
export function path(w, from, to, flies = false) {
  const [sx, sy] = cell(from),
    [tx, ty] = cell(to);
  if (!passable(w, tx, ty, flies)) return [];
  let q = [[sx, sy]],
    seen = new Map([[sy * W + sx, null]]),
    end = ty * W + tx;
  for (let i = 0; i < q.length; i++) {
    let [x, y] = q[i],
      key = y * W + x;
    if (key === end) break;
    for (let [dx, dy] of [
      [1, 0],
      [-1, 0],
      [0, 1],
      [0, -1],
    ]) {
      let nx = x + dx,
        ny = y + dy,
        k = ny * W + nx;
      if (passable(w, nx, ny, flies) && !seen.has(k)) {
        seen.set(k, key);
        q.push([nx, ny]);
      }
    }
  }
  if (!seen.has(end)) return [];
  let out = [],
    k = end;
  while (seen.get(k) !== null) {
    out.push({ x: (k % W) * TW + 24, y: Math.floor(k / W) * TH + 16 });
    k = seen.get(k);
  }
  out.reverse();
  // Cell routes alone cannot approach off-centre targets within the current cell.
  // Keep the precise destination as the final waypoint, including same-cell trips.
  if (distance(out.at(-1) ?? from, to) > 1) out.push({ x: to.x, y: to.y });
  return out;
}
export function move(w, e, dx, dy) {
  const pos = { x: e.x + dx, y: e.y + dy };
  if (passable(w, ...cell(pos), e.flies)) {
    e.x = pos.x;
    e.y = pos.y;
    if (dx || dy)
      e.dir = (Math.round((Math.atan2(-dy, dx) / Math.PI) * 4) + 8) % 8;
    return true;
  }
  return false;
}
export function action(w, actorId, targetId, verb) {
  const a = w.entities.find((e) => e.id === actorId),
    t = w.entities.find((e) => e.id === targetId);
  if (!a || a.kind !== "player" || !t || distance(a, t) > 85)
    return { ok: false, message: "Move closer to interact." };
  let msg = "";
  if (verb === "harvest" && t.kind === "bush" && t.food > 0) {
    t.food--;
    a.berries++;
    w.harvested++;
    msg = "Picked a berry. Leaving some keeps this patch useful to foragers.";
  } else if (verb === "gather" && t.kind === "wood" && t.amount > 0) {
    a.wood += t.amount;
    t.amount = 0;
    msg = "Collected fallen wood. Three wood builds a feeding station.";
  } else if (verb === "feed" && t.kind === "tray" && a.berries > 0) {
    a.berries--;
    t.food++;
    msg = "Stocked the feeding station. Hungry creatures can choose it.";
  } else if (verb === "inspect" && t.kind === "agent") {
    return {
      ok: true,
      message: `${t.name}: ${t.goal}. ${t.flies ? "Wings allow water crossings." : "Ground-bound; routes around water."} Meals: ${t.meals}.`,
    };
  } else return { ok: false, message: "That action is unavailable." };
  record(w, verb, msg, a.id, t.id);
  return { ok: true, message: msg };
}
export function build(w, verb) {
  const a = w.entities.find((e) => e.kind === "player");
  if (!["plant", "tray"].includes(verb))
    return { ok: false, message: "Unknown action." };
  if ((verb === "plant" && a.berries < 1) || (verb === "tray" && a.wood < 3))
    return {
      ok: false,
      message:
        verb === "plant"
          ? "Planting needs one berry."
          : "A station needs three wood.",
    };
  const pos = { x: a.x + 48, y: a.y };
  if (
    !passable(w, ...cell(pos)) ||
    w.entities.some((e) => e.kind !== "scenery" && distance(e, pos) < 35)
  )
    return { ok: false, message: "Find open ground to your right." };
  if (verb === "plant") a.berries--;
  else a.wood -= 3;
  const t = {
    id: w.nextId++,
    kind: verb === "plant" ? "bush" : "tray",
    ...pos,
    food: 0,
    regrow: 0,
    asset: "bush3",
  };
  w.entities.push(t);
  if (verb === "plant") w.planted++;
  const msg =
    verb === "plant"
      ? "Planted a new patch. Berries ripen in 20 seconds."
      : "Built a feeding station. Add berries to attract foragers.";
  record(w, verb, msg, a.id, t.id);
  return { ok: true, message: msg };
}
export function step(w, dt) {
  w.time += dt;
  for (const b of w.entities.filter((e) => e.kind === "bush")) {
    b.regrow += dt;
    if (b.regrow >= 20) {
      b.regrow -= 20;
      b.food = Math.min(4, b.food + 1);
    }
  }
  for (const a of w.entities.filter((e) => e.kind === "agent")) {
    a.moving = false;
    a.hunger = Math.min(1, a.hunger + dt * 0.012);
    if (a.hunger < 0.4) {
      a.goal = "Resting after a meal";
      a.target = null;
      a.route = null;
      continue;
    }
    let target = w.entities.find((e) => e.id === a.target && e.food > 0);
    if (!target) {
      a.target = null;
      a.route = null;
      const candidates = w.entities
        .filter((e) => (e.kind === "bush" || e.kind === "tray") && e.food > 0)
        .sort((b, c) => distance(a, b) - distance(a, c));
      for (const b of candidates) {
        let route = path(w, a, b, a.flies);
        if (route.length || distance(a, b) < 40) {
          target = b;
          a.target = b.id;
          a.route = route;
          break;
        }
      }
      a.goal = target
        ? `Seeking ${target.kind === "tray" ? "feeding station" : "berries"} #${target.id}`
        : "Waiting for reachable food";
    }
    if (!target) continue;
    if (distance(a, target) < 35) {
      target.food--;
      a.hunger = Math.max(0, a.hunger - 0.6);
      a.meals++;
      a.target = null;
      a.route = null;
      record(
        w,
        "meal",
        `${a.name} ate from ${target.kind === "tray" ? "a feeding station" : "a berry patch"} #${target.id}.`,
        a.id,
        target.id,
      );
      continue;
    }
    if (a.route?.length) {
      const n = a.route[0],
        dist = distance(a, n),
        len = Math.min(dist, a.speed * dt);
      if (dist < 2) {
        a.route.shift();
        continue;
      }
      a.moving = move(
        w,
        a,
        ((n.x - a.x) / dist) * len,
        ((n.y - a.y) / dist) * len,
      );
      if (dist < 2) a.route.shift();
    } else a.target = null;
  }
}
export function save(w) {
  return JSON.stringify({
    format: "mosslight",
    version: VERSION,
    seed: w.seed,
    state: {
      time: w.time,
      entities: w.entities,
      events: w.events,
      nextId: w.nextId,
      planted: w.planted,
      harvested: w.harvested,
    },
  });
}
export function load(raw) {
  const v = JSON.parse(raw);
  if (
    v.format !== "mosslight" ||
    v.version !== VERSION ||
    !Number.isInteger(v.seed) ||
    !v.state ||
    !Array.isArray(v.state.entities) ||
    !Number.isFinite(v.state.time)
  )
    throw Error("Unsupported or invalid save.");
  const w = generate(v.seed),
    s = v.state;
  const ids = new Set();
  for (const e of s.entities) {
    if (
      !Number.isInteger(e.id) ||
      ids.has(e.id) ||
      !Number.isFinite(e.x) ||
      !Number.isFinite(e.y) ||
      !passable(w, ...cell(e), e.flies)
    )
      throw Error("Invalid entity in save.");
    ids.add(e.id);
  }
  if (
    s.entities.filter((e) => e.kind === "player").length !== 1 ||
    !Number.isInteger(s.nextId) ||
    s.nextId <= Math.max(...ids) ||
    !Array.isArray(s.events)
  )
    throw Error("Invalid save state.");
  const nonnegative = (value) => Number.isFinite(value) && value >= 0;
  for (const e of s.entities) {
    if (
      !["player", "agent", "bush", "tray", "wood", "scenery"].includes(e.kind)
    )
      throw Error("Unknown saved entity.");
    if (["player", "agent"].includes(e.kind)) {
      if (
        !catalog[e.asset] ||
        typeof e.name !== "string" ||
        !Number.isInteger(e.dir) ||
        e.dir < 0 ||
        e.dir > 7
      )
        throw Error("Invalid saved creature.");
    }
    if (
      e.kind === "player" &&
      ![e.berries, e.wood].every((v) => Number.isInteger(v) && v >= 0)
    )
      throw Error("Invalid inventory.");
    if (e.kind === "agent") {
      if (
        !nonnegative(e.hunger) ||
        e.hunger > 1 ||
        e.flies !== catalog[e.asset].flies ||
        e.speed !== catalog[e.asset].speed ||
        typeof e.goal !== "string" ||
        !Number.isInteger(e.meals) ||
        e.meals < 0
      )
        throw Error("Invalid saved agent.");
      if (
        e.route &&
        (!Array.isArray(e.route) ||
          e.route.length > W * H ||
          e.route.some(
            (n) =>
              !n ||
              !Number.isFinite(n.x) ||
              !Number.isFinite(n.y) ||
              !passable(w, ...cell(n), e.flies),
          ))
      )
        throw Error("Invalid saved route.");
    }
    if (
      ["bush", "tray"].includes(e.kind) &&
      (!Number.isInteger(e.food) ||
        e.food < 0 ||
        (e.kind === "bush" && (e.food > 4 || !nonnegative(e.regrow))))
    )
      throw Error("Invalid saved food.");
    if (e.kind === "wood" && (!Number.isInteger(e.amount) || e.amount < 0))
      throw Error("Invalid saved wood.");
    if (
      ["bush", "scenery"].includes(e.kind) &&
      !/^(bush|tree|rock)[1-5]$/.test(e.asset)
    )
      throw Error("Invalid scenery asset.");
  }
  if (
    s.time < 0 ||
    ![s.planted, s.harvested].every((v) => Number.isInteger(v) && v >= 0) ||
    s.events.length > 100 ||
    s.events.some(
      (e) =>
        !e ||
        !nonnegative(e.time) ||
        typeof e.text !== "string" ||
        typeof e.type !== "string",
    )
  )
    throw Error("Invalid history.");
  // Only restore the owned save fields; generated terrain and version remain authoritative.
  return {
    ...w,
    time: s.time,
    entities: s.entities,
    events: s.events,
    nextId: s.nextId,
    planted: s.planted,
    harvested: s.harvested,
  };
}
