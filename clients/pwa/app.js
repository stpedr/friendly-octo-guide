// Core do cliente — o mesmo em Web (PWA), Desktop (Tauri) e Mobile (Tauri mobile).
// Fala SÓ com o Gateway: /v1/auth (login em duas etapas) e WSS de telemetria.
// Access token fica em memória (não em localStorage — XSS não pode roubá-lo);
// refresh token rotativo cuida da continuidade da sessão.

const GATEWAY = globalThis.GATEWAY_URL ?? "";

const state = {
  accessToken: null,
  refreshToken: null,
  challengeId: null,
  socket: null,
};

const $ = (sel) => document.querySelector(sel);
const show = (id) => {
  for (const sec of ["#login-step", "#totp-step", "#panel"]) $(sec).hidden = sec !== id;
};
const fail = (msg) => {
  const el = $("#error");
  el.textContent = msg;
  el.hidden = false;
  setTimeout(() => { el.hidden = true; }, 5000);
};

async function api(path, body) {
  const started = performance.now();
  const response = await fetch(`${GATEWAY}${path}`, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      ...(state.accessToken ? { authorization: `Bearer ${state.accessToken}` } : {}),
    },
    body: JSON.stringify(body),
  });
  emitRum(path, response.status, performance.now() - started);
  if (!response.ok) throw new Error(`${path} → ${response.status}`);
  return response.json();
}

// RUM mínimo: rota, status e duração viram beacon pro collector da espinha.
// (O OTel Collector expõe receiver HTTP pra eventos de front na fase 1.)
function emitRum(route, status, durationMs) {
  const beacon = JSON.stringify({ route, status, durationMs, ts: Date.now() });
  navigator.sendBeacon?.(`${GATEWAY}/rum`, beacon);
}

// ── Etapa 1: senha → desafio ─────────────────────────────────────
$("#login-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const form = new FormData(e.target);
  try {
    const { challengeId } = await api("/v1/auth/login", {
      username: form.get("username"),
      password: form.get("password"),
    });
    state.challengeId = challengeId;
    show("#totp-step");
    $("#totp-form code")?.focus();
  } catch {
    fail("Usuário ou senha inválidos.");
  }
});

// ── Etapa 2: TOTP → sessão ───────────────────────────────────────
$("#totp-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const form = new FormData(e.target);
  try {
    const session = await api("/v1/auth/totp", {
      challengeId: state.challengeId,
      code: form.get("code"),
    });
    state.accessToken = session.accessToken;
    state.refreshToken = session.refreshToken;
    $("#user-badge").textContent = claimsOf(session.accessToken).sub ?? "";
    show("#panel");
    connectTelemetry();
  } catch {
    fail("Código recusado — confira o authenticator.");
  }
});

$("#logout").addEventListener("click", () => {
  state.accessToken = state.refreshToken = null;
  state.socket?.close();
  show("#login-step");
});

function claimsOf(jwt) {
  try {
    return JSON.parse(atob(jwt.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
  } catch {
    return {};
  }
}

// ── Painel ao vivo: WSS, não polling ─────────────────────────────
function connectTelemetry() {
  const url = `${GATEWAY.replace(/^http/, "ws") || `ws://${location.host}`}/v1/linha/ws`;
  const socket = new WebSocket(url);
  state.socket = socket;

  socket.addEventListener("open", () => {
    $("#conn-state").textContent = "telemetria ao vivo";
    socket.send(JSON.stringify({ authorization: `Bearer ${state.accessToken}` }));
  });
  socket.addEventListener("message", (event) => renderReading(JSON.parse(event.data)));
  socket.addEventListener("close", () => {
    $("#conn-state").textContent = "reconectando…";
    if (state.accessToken) setTimeout(connectTelemetry, 3000);
  });
}

const tiles = new Map();
function renderReading({ sensorId, value, measuredAt }) {
  let tile = tiles.get(sensorId);
  if (!tile) {
    tile = document.createElement("div");
    tile.className = "sensor";
    tile.innerHTML = `<h2></h2><p class="value"></p><p class="ts hint"></p>`;
    tile.querySelector("h2").textContent = sensorId;
    $("#sensors").append(tile);
    tiles.set(sensorId, tile);
  }
  tile.querySelector(".value").textContent = Number(value).toFixed(2);
  tile.querySelector(".ts").textContent = new Date(measuredAt).toLocaleTimeString("pt-BR");
}

// ── Shell PWA ────────────────────────────────────────────────────
if ("serviceWorker" in navigator) navigator.serviceWorker.register("sw.js");
show("#login-step");
