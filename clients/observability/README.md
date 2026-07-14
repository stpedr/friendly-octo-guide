# Cliente de observabilidade (Tauri)

O dashboard de observabilidade segue a MESMA regra de todo sistema com interface
(bloco 1): um core web, três shells. Aqui o core embute o **Grafana** e adiciona
o que só um app nativo dá — **push nativo de alerta**, **cache offline** e
**desbloqueio via TOTP local**.

- `web/` — core web (unlock TOTP → Grafana embutido → banner offline). Roda como
  PWA sozinho; o TOTP (RFC 6238) é verificado localmente com Web Crypto
  (`web/totp.js`), validado contra os vetores de teste da RFC.
- `src-tauri/` — shell Tauri (desktop + mobile), com `tauri-plugin-notification`
  pro push nativo. `src/lib.rs` é o núcleo compartilhado; `src/main.rs` só chama `run()`.

## Rodar

```bash
# só o core web (dev):
cd clients/observability/web && python3 -m http.server 8085   # http://localhost:8085

# shell nativo:
cd clients/observability/src-tauri
PLATAFORMA_GRAFANA_URL=http://<ip-do-grafana>:3000 cargo tauri dev
```

Sem `PLATAFORMA_GRAFANA_URL`, cai em `http://localhost:3000`. O primeiro uso pede
a seed Base32 do authenticator (guardada só neste dispositivo); depois, o código
de 6 dígitos desbloqueia.

## Dependências de sistema (Linux) e build

Iguais ao shell principal (`clients/desktop/README.md`): WebKitGTK + GTK, e
`cargo tauri build` gera AppImage + `.deb`. Não compilado neste ambiente (o
shell principal foi; este espelha a mesma estrutura, com o plugin de notificação).
