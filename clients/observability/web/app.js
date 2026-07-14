// Shell do dashboard de observabilidade: desbloqueio TOTP local → embute o
// Grafana → banner offline. O endereço do Grafana vem do shell nativo
// (window.GRAFANA_URL), com fallback pra dev. Mesmo padrão do PWA principal.
const GRAFANA = (typeof window !== "undefined" && window.GRAFANA_URL) || "http://localhost:3000";
const SEED_KEY = "obs.totp.seed";

const $ = (sel) => document.querySelector(sel);

function unlock() {
  $("#lock").hidden = true;
  $("#dash").hidden = false;
  // Só aponta o iframe depois de desbloquear — nada carrega antes do 2FA.
  $("#grafana").src = GRAFANA;
}

async function tryUnlock(code) {
  const seed = localStorage.getItem(SEED_KEY);
  if (!seed) {
    $("#lock-msg").textContent = "Nenhuma seed registrada neste dispositivo — abra “Primeiro uso”.";
    return;
  }
  if (await window.TOTP.verify(seed, code)) {
    unlock();
  } else {
    $("#lock-msg").textContent = "Código inválido. Tente de novo.";
  }
}

if (typeof document !== "undefined") {
  $("#unlock-form")?.addEventListener("submit", (e) => {
    e.preventDefault();
    tryUnlock($("#totp").value.trim());
  });

  $("#save-seed")?.addEventListener("click", () => {
    const seed = $("#seed").value.trim();
    try {
      window.TOTP.base32Decode(seed); // valida antes de guardar
      localStorage.setItem(SEED_KEY, seed);
      $("#lock-msg").textContent = "Seed salva. Desbloqueie com o código atual.";
      $("#enroll").open = false;
    } catch {
      $("#lock-msg").textContent = "Seed Base32 inválida.";
    }
  });

  // Banner offline: métricas em cache continuam visíveis quando a rede cai.
  const offline = $("#offline");
  const sync = () => { if (offline) offline.hidden = navigator.onLine; };
  window.addEventListener("online", sync);
  window.addEventListener("offline", sync);
  sync();

  // Cache do shell pra abrir offline (o iframe do Grafana depende de rede, mas
  // o app abre e mostra o último estado conhecido).
  if ("serviceWorker" in navigator) {
    navigator.serviceWorker.register("sw.js").catch(() => {});
  }
}

// Exporta pra teste (Node) sem quebrar no browser.
if (typeof module !== "undefined" && module.exports) {
  module.exports = { GRAFANA, SEED_KEY };
}
