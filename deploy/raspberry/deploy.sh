#!/usr/bin/env bash
# Deploy da plataforma na Raspberry Pi. Rode NA PI (via SSH), a partir de qualquer pasta:
#   curl -fsSL https://raw.githubusercontent.com/stpedr/friendly-octo-guide/claude/dotnet-platform-architecture-p37juw/deploy/raspberry/deploy.sh | bash
# ou, com o repo já clonado:  ./deploy/raspberry/deploy.sh [perfis extras...]
set -euo pipefail

REPO_URL="https://github.com/stpedr/friendly-octo-guide.git"
BRANCH="claude/dotnet-platform-architecture-p37juw"
DIR="$HOME/plataforma-linha"

# ── Pré-requisitos ────────────────────────────────────────────────
if ! command -v docker >/dev/null 2>&1; then
  echo ">> Docker não encontrado — instalando (script oficial)..."
  curl -fsSL https://get.docker.com | sh
  sudo usermod -aG docker "$USER"
  echo ">> Docker instalado. Saia e entre de novo na sessão SSH (pro grupo docker valer) e rode este script outra vez."
  exit 0
fi

ARCH="$(uname -m)"
if [[ "$ARCH" != "aarch64" && "$ARCH" != "arm64" ]]; then
  echo ">> Aviso: arquitetura '$ARCH' — este compose foi pensado pra Pi 64-bit (aarch64)."
  echo "   Em Pi 32-bit (armv7l) as imagens .NET/Kafka não existem: reinstale o Raspberry Pi OS 64-bit."
fi

# ── Código ────────────────────────────────────────────────────────
if [[ -d "$DIR/.git" ]]; then
  echo ">> Atualizando $DIR..."
  git -C "$DIR" fetch origin "$BRANCH"
  git -C "$DIR" checkout "$BRANCH"
  git -C "$DIR" pull --ff-only origin "$BRANCH"
else
  echo ">> Clonando $REPO_URL ($BRANCH) em $DIR..."
  git clone --branch "$BRANCH" --depth 1 "$REPO_URL" "$DIR"
fi

# ── Sobe ──────────────────────────────────────────────────────────
# Perfis extras via argumentos: ./deploy.sh simulador observabilidade-completa
PROFILES=()
for p in "$@"; do PROFILES+=(--profile "$p"); done

cd "$DIR"
echo ">> Build + up (primeiro build compila 8 serviços .NET na Pi — paciência, ~10-20 min)..."
docker compose -f deploy/raspberry/docker-compose.pi.yml "${PROFILES[@]}" up -d --build

IP="$(hostname -I | awk '{print $1}')"
echo
echo "── Pronto ───────────────────────────────────────────────────"
echo "Gateway (API):    http://$IP:8180/healthz"
echo "Grafana:          http://$IP:3030"
echo "ntfy (push):      http://$IP:8090  → assine o tópico 'oncall-primario' no app"
echo "MQTT (sensores):  $IP:1883        → tópico linha/<linha>/sensor/<id>"
echo
echo "Painel vivo sem hardware: rode com o perfil simulador →"
echo "  ./deploy/raspberry/deploy.sh simulador"
