# Clientes — um core, três shells

Web, desktop e mobile rodam o **mesmo bundle** (`pwa/index.html` + `app.js` +
`styles.css`), sem framework de UI e sem build step — é só HTML/CSS/JS que
fala com o Gateway (`/v1/auth`, WSS de telemetria) e nada mais.

| Shell | Diretório | Empacotamento |
|---|---|---|
| Web (PWA instalável) | [`pwa/`](pwa) | Service worker + manifest, servido pelo próprio Gateway/CDN |
| Desktop (Win/macOS/Linux) | [`desktop/`](desktop) | Tauri — WebView do SO, sem Electron |
| Mobile (Android/iOS) | [`mobile/`](mobile) | Mesmo crate Tauri do desktop, `cargo tauri android/ios` |

`desktop/src-tauri` carrega `pwa/` direto (`frontendDist` aponta pra lá) — mudar
o core em `pwa/app.js` já reflete em todo shell, sem duplicar código nem
precisar de um passo de sincronização.

O que muda entre os três é só a casca: desktop/mobile injetam
`window.GATEWAY_URL` via variável de ambiente (`PLATAFORMA_GATEWAY_URL`) na
inicialização do WebView; a Web usa o mesmo domínio de onde foi servida
(`GATEWAY_URL` vazio → `fetch` relativo).
