#!/bin/bash
# Bootstrap do nó SERVER do cluster k3s (rodar no pc-pedro, como root).
# Instala: k3s + ArgoCD + KEDA + Linkerd (mTLS) + External Secrets + Velero.
# Idempotente: rodar de novo só atualiza o que faltar.
set -euo pipefail

command -v curl >/dev/null || { echo "instale curl antes"; exit 1; }

# ── k3s server ────────────────────────────────────────────────────
# --disable traefik: a borda da plataforma é o Gateway (e o WAF na frente dele).
if ! command -v k3s >/dev/null; then
  curl -sfL https://get.k3s.io | INSTALL_K3S_EXEC="server --disable traefik" sh -
fi
export KUBECONFIG=/etc/rancher/k3s/k3s.yaml
until kubectl get nodes >/dev/null 2>&1; do sleep 2; done
echo "k3s server no ar: $(kubectl get nodes --no-headers | wc -l) nó(s)"

# ── Helm (instala local se faltar) ────────────────────────────────
if ! command -v helm >/dev/null; then
  curl -sfL https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
fi

# ── ArgoCD: o GitOps que aplica deploy/helm/plataforma-linha ─────
kubectl create namespace argocd --dry-run=client -o yaml | kubectl apply -f -
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
echo "senha inicial do ArgoCD (usuário admin):"
kubectl -n argocd get secret argocd-initial-admin-secret \
  -o jsonpath='{.data.password}' 2>/dev/null | base64 -d || echo "(aparece quando o pod subir)"

# ── KEDA: dá vida aos ScaledObject do chart (lag de Kafka) ───────
helm repo add kedacore https://kedacore.github.io/charts >/dev/null
helm repo update >/dev/null
helm upgrade --install keda kedacore/keda -n keda --create-namespace

# ── Linkerd: mTLS automático entre os pods injetados ─────────────
if ! command -v linkerd >/dev/null; then
  curl -sfL https://run.linkerd.io/install-edge | sh
  export PATH="$PATH:$HOME/.linkerd2/bin"
fi
linkerd check --pre
linkerd install --crds | kubectl apply -f -
linkerd install | kubectl apply -f -

# ── External Secrets Operator: segredos saem do OpenBao ──────────
helm repo add external-secrets https://charts.external-secrets.io >/dev/null
helm upgrade --install external-secrets external-secrets/external-secrets \
  -n external-secrets --create-namespace

# ── Velero: backup do cluster pro MinIO ──────────────────────────
helm repo add vmware-tanzu https://vmware-tanzu.github.io/helm-charts >/dev/null
helm upgrade --install velero vmware-tanzu/velero -n velero --create-namespace \
  --set configuration.backupStorageLocation[0].name=default \
  --set configuration.backupStorageLocation[0].provider=aws \
  --set configuration.backupStorageLocation[0].bucket=cluster-backups \
  --set configuration.backupStorageLocation[0].config.region=minio \
  --set configuration.backupStorageLocation[0].config.s3ForcePathStyle=true \
  --set configuration.backupStorageLocation[0].config.s3Url=http://minio.dados:9000 \
  --set "initContainers[0].name=velero-plugin-for-aws" \
  --set "initContainers[0].image=velero/velero-plugin-for-aws:v1.11.0" \
  --set "initContainers[0].volumeMounts[0].mountPath=/target" \
  --set "initContainers[0].volumeMounts[0].name=plugins" \
  --set credentials.secretContents.cloud="[default]
aws_access_key_id=dev
aws_secret_access_key=devdevdev"

echo
echo "══════════════════════════════════════════════════════════════"
echo "Server pronto. Pra juntar os agents (Pi/Jetson), rode NELES:"
echo
echo "  sudo K3S_URL=https://$(hostname -I | awk '{print $1}'):6443 \\"
echo "       K3S_TOKEN=$(cat /var/lib/rancher/k3s/server/node-token) \\"
echo "       bash deploy/cluster/bootstrap-agent.sh"
echo
echo "Depois: kubectl apply -f deploy/argocd/project.yaml -f deploy/argocd/application.yaml"
echo "══════════════════════════════════════════════════════════════"
