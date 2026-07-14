#!/bin/sh
# Publica todos os .avsc de schemas/ no Apicurio Registry (API v3).
# Idempotente: ifExists=FIND_OR_CREATE_VERSION não duplica versão igual.
set -eu

REGISTRY="${REGISTRY_URL:-http://schema-registry:8080}"
GROUP="${SCHEMA_GROUP:-plataforma-linha}"

until curl -sf "$REGISTRY/health/ready" > /dev/null; do
  echo "aguardando o schema-registry..."
  sleep 2
done

for file in /schemas/*.avsc; do
  artifact="$(basename "$file" .avsc)"
  echo "registrando $artifact"
  # jq não existe na imagem curl; o wrapper JSON é montado com sed (escape de aspas).
  content="$(sed 's/\\/\\\\/g; s/"/\\"/g' "$file" | tr -d '\n')"
  curl -sf -X POST \
    -H "Content-Type: application/json" \
    "$REGISTRY/apis/registry/v3/groups/$GROUP/artifacts?ifExists=FIND_OR_CREATE_VERSION" \
    -d "{
      \"artifactId\": \"$artifact\",
      \"artifactType\": \"AVRO\",
      \"firstVersion\": { \"content\": { \"content\": \"$content\", \"contentType\": \"application/json\" } }
    }" > /dev/null
done
echo "schemas registrados no grupo $GROUP"
