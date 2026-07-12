#!/bin/sh
# Roda em TODO `docker compose up` (não só no primeiro boot): o OpenBao volta selado a
# cada restart do container dele (sem auto-unseal via KMS — não temos um na Pi), então
# este script reabre o cofre com a chave persistida antes que Identity/Gateway tentem ler.
#
# Simplificação consciente de home-lab: token de aplicação com ID fixo (via
# OPENBAO_APP_TOKEN) em vez de AppRole/least-privilege dinâmico — produção de verdade
# trocaria isso por autenticação por AppRole com TTL curto.
set -eu

export BAO_ADDR=http://openbao:8200
STATE_DIR=/state
mkdir -p "$STATE_DIR"

echo ">> Esperando o OpenBao responder..."
until bao status >/tmp/status.out 2>&1 || grep -q "Sealed" /tmp/status.out 2>/dev/null; do
  sleep 2
done

if [ ! -f "$STATE_DIR/unseal.key" ]; then
  echo ">> Primeiro boot: inicializando (1 key share — home-lab, sem quórum multi-operador)..."
  INIT_OUT=$(bao operator init -key-shares=1 -key-threshold=1)
  echo "$INIT_OUT" | grep "Unseal Key 1:" | awk '{print $NF}' > "$STATE_DIR/unseal.key"
  echo "$INIT_OUT" | grep "Initial Root Token:" | awk '{print $NF}' > "$STATE_DIR/root.token"
fi

SEALED=$(bao status 2>/dev/null | grep "^Sealed" | awk '{print $2}' || echo "true")
if [ "$SEALED" = "true" ]; then
  echo ">> Reabrindo o cofre (unseal)..."
  bao operator unseal "$(cat "$STATE_DIR/unseal.key")" >/dev/null
fi

export BAO_TOKEN
BAO_TOKEN=$(cat "$STATE_DIR/root.token")

bao secrets list 2>/dev/null | grep -q '^secret/' || bao secrets enable -path=secret kv-v2

if [ ! -f "$STATE_DIR/jwt.key" ]; then
  head -c32 /dev/urandom | od -An -tx1 | tr -d ' \n' > "$STATE_DIR/jwt.key"
fi
if [ ! -f "$STATE_DIR/keycloak-client-secret" ]; then
  head -c32 /dev/urandom | od -An -tx1 | tr -d ' \n' > "$STATE_DIR/keycloak-client-secret"
fi
JWT_KEY=$(cat "$STATE_DIR/jwt.key")
KC_SECRET=$(cat "$STATE_DIR/keycloak-client-secret")

bao kv put secret/platform/jwt signingKey="$JWT_KEY" >/dev/null
bao kv put secret/platform/keycloak clientId=identity-api clientSecret="$KC_SECRET" >/dev/null

cat > /tmp/platform-read.hcl <<'EOF'
path "secret/data/platform/*" {
  capabilities = ["read"]
}
EOF
bao policy write platform-read /tmp/platform-read.hcl >/dev/null

bao token create -policy=platform-read -id="$OPENBAO_APP_TOKEN" -orphan -period=768h >/dev/null 2>&1 || true

echo ">> Renderizando realm-export do Keycloak com o client secret estável..."
sed "s/__CLIENT_SECRET__/$KC_SECRET/" /template/realm-export.json.template > /kc-import/realm-export.json

echo ">> OpenBao pronto: kv em secret/platform/*, token de app fixo, realm do Keycloak renderizado."
