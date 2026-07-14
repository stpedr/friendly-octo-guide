// Cache offline do shell de observabilidade: o app abre sem rede e mostra o
// último painel conhecido. NÃO cacheia o Grafana em si (vem por rede) nem
// nenhuma chamada /v1/** — dado quente nunca fica preso em cache.
const CACHE = "obs-shell-v1";
const SHELL = ["index.html", "styles.css", "app.js", "totp.js", "manifest.webmanifest"];

self.addEventListener("install", (e) => {
  e.waitUntil(caches.open(CACHE).then((c) => c.addAll(SHELL)));
  self.skipWaiting();
});

self.addEventListener("activate", (e) => {
  e.waitUntil(
    caches.keys().then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k)))),
  );
  self.clients.claim();
});

self.addEventListener("fetch", (e) => {
  const url = new URL(e.request.url);
  if (url.pathname.includes("/v1/")) return; // nunca cacheia API
  e.respondWith(
    caches.match(e.request).then((hit) => hit || fetch(e.request).catch(() => caches.match("index.html"))),
  );
});
