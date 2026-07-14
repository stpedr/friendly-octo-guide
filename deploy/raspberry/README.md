# Deploy na Raspberry Pi

A Pi vira o **edge da plataforma na sua rede**: broker MQTT recebendo sensores,
gateway de protocolo com store-and-forward, ingest com quality gate, scoring
preditivo, alertas com push no celular (ntfy) e Grafana — tudo em contêiner ARM64.

## Requisitos

- Raspberry Pi 4 ou 5, **SO 64-bit** (aarch64) — as imagens .NET/Kafka não existem em 32-bit
- 8 GB de RAM pro perfil padrão (≈ 3,5 GB usados); com 4 GB, suba o subconjunto indicado no compose
- Cartão/SSD com ~10 GB livres

## Passo a passo (da sua máquina)

```bash
# 1. Entre na Pi (troque pelo IP dela se o mDNS não resolver)
ssh maktheus@raspberrypi.local

# 2. Na Pi, rode o instalador — ele instala Docker se faltar, clona a branch e sobe tudo
curl -fsSL https://raw.githubusercontent.com/stpedr/friendly-octo-guide/claude/dotnet-platform-architecture-p37juw/deploy/raspberry/deploy.sh | bash
```

> Se o Docker for instalado nessa primeira execução, o script pede pra você
> relogar (grupo `docker`) e rodar de novo — é esperado.

Primeiro build compila os 8 serviços .NET na própria Pi: **10–20 min**. Depois
disso, atualizações são incrementais.

## Depois de subir

| Serviço | Endereço |
|---|---|
| Gateway (API da plataforma) | `http://<ip-da-pi>:8180` |
| Grafana (espinha de observabilidade) | `http://<ip-da-pi>:3030` |
| ntfy (push de alerta) | `http://<ip-da-pi>:8090` — instale o app ntfy e assine `oncall-primario` |
| MQTT (sensores reais) | `<ip-da-pi>:1883` — tópico `linha/<linha>/sensor/<id>` |

Pra ver o fluxo inteiro vivo sem hardware nenhum, suba com o simulador de sensor:

```bash
~/plataforma-linha/deploy/raspberry/deploy.sh simulador
```

Ele publica temperatura fake no MQTT a cada 2 s → edge → Kafka → quality gate →
Postgres, e quando o valor foge do regime o Predictive dispara alerta → ntfy.

Perfis extras: `observabilidade-completa` (Loki + Tempo) e `ia`
(router/worker/chatbot — exigem um vLLM em outra máquina, via `VLLM_BASE_URL`).

## Operação

```bash
cd ~/plataforma-linha
docker compose -f deploy/raspberry/docker-compose.pi.yml ps       # estado
docker compose -f deploy/raspberry/docker-compose.pi.yml logs -f edge-protocol-gateway
docker compose -f deploy/raspberry/docker-compose.pi.yml down     # parar tudo (volumes ficam)
```

## Segurança na rede doméstica

- O compose expõe só o necessário (8180/3030/8090/1883). Nada disso deve ser
  visível da internet — se quiser acesso externo, use VPN (WireGuard/Tailscale),
  não port-forward.
- Grafana está anônimo por conveniência de rede local; ative senha se a rede
  for compartilhada.
- **Credenciais nunca em chat/commit**: se uma senha da Pi já circulou em texto
  plano, troque-a (`passwd`) e prefira chave SSH (`ssh-copy-id maktheus@<ip>`).
