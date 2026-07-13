# Shell desktop (Tauri)

Mesmo core web de `clients/pwa`, empacotado como binário nativo (Win/macOS/Linux)
via [Tauri](https://tauri.app) — sem Electron, WebView do próprio SO.

`src-tauri/src/lib.rs` é o núcleo compartilhado com o shell mobile
(`clients/mobile/`); `src/main.rs` só chama `run()`.

## Rodar em dev

```bash
cd clients/desktop/src-tauri
PLATAFORMA_GATEWAY_URL=http://<ip-do-gateway>:8180 cargo tauri dev
```

Sem `PLATAFORMA_GATEWAY_URL`, cai em `http://localhost:8180`.

## Build de produção

```bash
cd clients/desktop/src-tauri
cargo tauri build
```

Gera AppImage + `.deb` (Linux — ver `targets` em `tauri.conf.json`; adicione
`msi`/`dmg` pra Windows/macOS ao empacotar nessas plataformas). O binário lê
`PLATAFORMA_GATEWAY_URL` do ambiente em tempo de execução — a mesma build
serve qualquer Gateway da rede, não precisa recompilar por IP.

## Dependências de sistema (Linux)

```bash
apt-get install -y libwebkit2gtk-4.1-dev libgtk-3-dev \
  libayatana-appindicator3-dev librsvg2-dev build-essential libssl-dev pkg-config
```

## Pendente

- Endereço do Gateway fixo em env var — o natural é uma tela de configuração
  na primeira execução (o mobile não tem env var de shell pra usar).
- Ícones em `src-tauri/icons/` são placeholders sólidos; trocar pela arte final.
