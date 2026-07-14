# LLM na Jetson

A Jetson é o nó de GPU da plataforma: ela serve um endpoint OpenAI-compatible
em `:8000` e **todo o resto já está pronto pra consumi-lo** — `ai-worker-llm`
e `chatbot` leem `VLLM_BASE_URL`, e o Knowledge aceita `Embeddings__BaseUrl`
pro mesmo contrato.

## Qual perfil usar

| Jetson | Perfil | Runtime | Por quê |
|---|---|---|---|
| **Nano (4 GB, JetPack 4)** | `nano` | llama.cpp | vLLM exige CUDA moderna; a Nano não tem. llama.cpp roda um modelo pequeno quantizado (Q4) com folga |
| **Orin (JetPack 6+)** | `orin` | vLLM (imagem NVIDIA p/ Jetson) | batching contínuo de verdade, o que a arquitetura chama de "GPU pool" |

## Subindo (na Jetson)

```bash
mkdir -p deploy/jetson/models
# Nano: baixe um GGUF pequeno (ex. Qwen2.5-1.5B-Instruct Q4_K_M, ~1 GB) pra models/
docker compose -f deploy/jetson/docker-compose.jetson.yml --profile nano up -d

# teste o contrato:
curl -s http://localhost:8000/v1/models
```

## Conectando a plataforma

No host que roda o compose da plataforma (Pi ou a própria Jetson):

```bash
VLLM_BASE_URL=http://<ip-da-jetson>:8000 \
  docker compose -f deploy/raspberry/docker-compose.pi.yml --profile ia up -d
```

No cluster k3s: o values do chart já aponta `Vllm__BaseUrl` pro Service
`vllm-llama.ia` — crie um Service/Endpoint apontando pro IP da Jetson, ou
rode a Jetson como nó do cluster com o label `gpu=nvidia` (ver `deploy/cluster/`).

## Aviso de recurso (Nano 4 GB)

A Nano não roda o LLM **e** a plataforma inteira ao mesmo tempo. Use-a só pro
LLM (este compose) e deixe a plataforma na Pi/pc-pedro — exatamente a divisão
que o perfil `ia` do compose da Pi assume.
