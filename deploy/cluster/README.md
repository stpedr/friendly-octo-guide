# Cluster k3s do homelab: o caminho da fase 2

Tudo da fase 2 que depende de "um cluster de verdade" roda em **k3s** — o
Kubernetes enxuto da CNCF que cabe nas máquinas que você já tem. Este diretório
tem os scripts que transformam suas máquinas nesse cluster.

## Topologia recomendada com o hardware atual

| Máquina | Papel | Por quê |
|---|---|---|
| **pc-pedro** (x86, mais RAM) | **server** (control-plane + worker) | etcd/API server são os mais famintos; fica na máquina mais forte |
| **Raspberry Pi 4 8GB** | agent (worker) | roda os serviços .NET leves; ARM64 é suportado nativamente |
| **Jetson** (Nano/Orin) | agent (worker, com GPU) | recebe o label `gpu=nvidia` e serve o LLM (ver `deploy/jetson/`) |

Requisitos por nó: Linux 64-bit, 2 GB+ RAM livre, `curl`, portas 6443/8472/10250
abertas entre os nós (LAN doméstica normal já atende).

## Passo a passo

```bash
# 1. No pc-pedro (server):
sudo bash deploy/cluster/bootstrap-server.sh
#    → instala k3s server, ArgoCD, KEDA, Linkerd (mTLS), External Secrets e Velero
#    → imprime o TOKEN e o comando de join dos agents

# 2. Em cada agent (Pi, Jetson), com o token do passo 1:
sudo K3S_URL=https://<ip-do-pc-pedro>:6443 K3S_TOKEN=<token> \
  bash deploy/cluster/bootstrap-agent.sh

# 3. Marque a Jetson como nó de GPU (no server):
kubectl label node <nome-do-no-jetson> gpu=nvidia

# 4. Aponte o GitOps pro repo (uma vez):
kubectl apply -f deploy/argocd/project.yaml -f deploy/argocd/application.yaml
#    → a partir daqui, deploy = merge na main; o ArgoCD converge o cluster sozinho
```

Pra ativar o mTLS entre os serviços, instale a plataforma com o mesh ligado
(o ArgoCD já faz isso se `mesh.enabled: true` estiver no values):

```yaml
# deploy/helm/plataforma-linha/values.yaml
mesh:
  enabled: true   # injeta o proxy Linkerd em todo pod → mTLS automático
```

## O que cada peça resolve

- **k3s**: o cluster em si — HA de workload, scheduling entre as máquinas,
  reinício automático. Um binário por nó, atualização com um comando.
- **ArgoCD**: GitOps — o cluster converge pro que está commitado (`deploy/argocd/`).
- **KEDA**: os `ScaledObject` do chart passam a valer (scale por lag de Kafka,
  incluindo scale-to-zero do worker de LLM).
- **Linkerd**: mTLS automático entre todos os pods injetados + métricas de
  rede por serviço. Zero mudança de código.
- **External Secrets Operator**: as conexões com senha saem do OpenBao e
  entram como Secret do K8s (os values do chart já assumem isso).
- **Velero**: backup dos objetos do cluster + volumes pro MinIO
  (agendamento em `deploy/dr/velero-schedule.yaml`).

## Registry: GHCR hoje, Harbor quando quiser

O CI publica imagens assinadas no **GHCR** — funciona sem nenhum servidor seu.
Harbor (registry self-hosted com Trivy embutido) só vale quando você quiser
imagens 100% dentro de casa:

```bash
helm repo add harbor https://helm.goharbor.io
helm install harbor harbor/harbor -n harbor --create-namespace \
  --set expose.type=clusterIP --set persistence.persistentVolumeClaim.registry.size=20Gi
# depois: trocar image.registry no values.yaml do chart e o login no CI
```

## Verificação

```bash
kubectl get nodes -o wide            # 3 nós Ready
kubectl -n argocd get applications   # plataforma-linha Synced/Healthy
linkerd check                        # mTLS ok
velero backup get                    # backups acontecendo
```
