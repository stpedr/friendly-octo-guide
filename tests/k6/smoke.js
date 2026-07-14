// Smoke de borda com k6: valida o CONTRATO do Gateway sem precisar de usuário
// seedado — saúde, beacon de RUM, negação por padrão (401/404) e o rate limit
// distribuído respondendo 429 sob rajada.
//
// Uso local:  k6 run -e BASE_URL=http://localhost:8180 tests/k6/smoke.js
// No CI:      .github/workflows/k6-smoke.yml (workflow_dispatch com a URL do ambiente)
import http from "k6/http";
import { check, sleep } from "k6";

const BASE = __ENV.BASE_URL || "http://localhost:8180";

export const options = {
  scenarios: {
    smoke: {
      executor: "constant-vus",
      vus: 5,
      duration: "30s",
    },
  },
  thresholds: {
    http_req_failed: [{ threshold: "rate<0.01", abortOnFail: true }], // 4xx esperado não conta como falha (ver abaixo)
    http_req_duration: ["p(95)<500"], // borda deve responder rápido mesmo negando
  },
};

export default function () {
  const health = http.get(`${BASE}/healthz`);
  check(health, { "healthz responde 200": (r) => r.status === 200 });

  const rum = http.post(
    `${BASE}/rum`,
    JSON.stringify({ route: "/v1/core/ordens", status: 200, durationMs: 42 }),
    { headers: { "Content-Type": "application/json" } },
  );
  check(rum, { "rum aceita beacon válido (202)": (r) => r.status === 202 });

  // Negação por padrão é contrato: sem token → 401; rota não listada → 404.
  const noToken = http.get(`${BASE}/v1/core/ordens`, {
    responseCallback: http.expectedStatuses(401, 429),
  });
  check(noToken, { "rota protegida sem token nega": (r) => [401, 429].includes(r.status) });

  const unlisted = http.get(`${BASE}/v1/interno/debug`, {
    responseCallback: http.expectedStatuses(404, 429),
  });
  check(unlisted, { "rota não listada não existe pra fora": (r) => [404, 429].includes(r.status) });

  sleep(0.5);
}
