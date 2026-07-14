# Shell mobile (Tauri)

Não é um projeto Rust separado: no Tauri v2, mobile e desktop compartilham
**o mesmo crate** — `clients/desktop/src-tauri` (`lib.rs::run()`, já anotado
com `#[cfg_attr(mobile, tauri::mobile_entry_point)]`). Este diretório existe
só pra documentar o que falta pra gerar os projetos nativos; não há nada
pra rodar sem o SDK correto instalado (Android Studio/NDK ou Xcode — nenhum
dos dois existe neste ambiente de deploy, por isso não foi gerado aqui).

## Android

Pré-requisitos: Android Studio + NDK, `ANDROID_HOME`/`NDK_HOME` configurados.

```bash
cd clients/desktop/src-tauri
cargo tauri android init      # gera gen/android/ (projeto Gradle) — uma vez só
PLATAFORMA_GATEWAY_URL=http://<ip-do-gateway>:8180 cargo tauri android dev
cargo tauri android build     # gera o .apk/.aab de release
```

## iOS

Pré-requisitos: macOS + Xcode (não dá pra gerar nem buildar fora do macOS).

```bash
cd clients/desktop/src-tauri
cargo tauri ios init          # gera gen/apple/ (projeto Xcode) — uma vez só
PLATAFORMA_GATEWAY_URL=http://<ip-do-gateway>:8180 cargo tauri ios dev
cargo tauri ios build
```

## Pendente

- Rodar `cargo tauri android init` / `ios init` de verdade num ambiente com o
  SDK (esta sessão não tinha nenhum dos dois) e commitar `gen/android` /
  `gen/apple` — hoje só o crate compartilhado existe.
- Mobile não tem shell pra `PLATAFORMA_GATEWAY_URL`: precisa de uma tela de
  configuração no primeiro uso (mesmo pendente anotado no README do desktop).
- Push nativo do alerta (ntfy) e desbloqueio via TOTP local — ver arquitetura,
  bloco "06 · Cliente nativo" — ainda não implementados no shell.
