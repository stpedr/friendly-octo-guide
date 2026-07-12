# Cliente — core web único, três shells

Fase 0: PWA vanilla (sem build step — o que está aqui é o que é servido).
Login em duas etapas contra o Gateway (`/v1/auth/login` → `/v1/auth/totp`),
painel de linha por WebSocket, RUM via `sendBeacon`.

Decisões que os shells herdam:

- **Access token só em memória** — XSS não rouba o que não está em `localStorage`.
- **Service worker cacheia a casca, nunca `/v1/**`** — telemetria velha exibida
  como "ao vivo" é pior que painel fora do ar.
- **Um design system** — tokens CSS no `:root` de `styles.css`.

Fase 1: shells Tauri (desktop/mobile) embrulham exatamente estes arquivos;
push nativo de alerta e desbloqueio TOTP local entram no shell, não no core.

Dev local: `python3 -m http.server 8081` nesta pasta (ou qualquer servidor
estático), com o Gateway em `http://localhost:8080` — defina
`globalThis.GATEWAY_URL` num `<script>` antes do `app.js` se as origens diferirem.
