# Add-ons de plataforma como código — a alternativa declarativa ao
# deploy/cluster/bootstrap-server.sh (que é o caminho imperativo/rápido).
# Escolha UM dos dois; não rode os dois no mesmo cluster.

resource "helm_release" "keda" {
  name             = "keda"
  namespace        = "keda"
  create_namespace = true
  repository       = "https://kedacore.github.io/charts"
  chart            = "keda"
}

resource "helm_release" "external_secrets" {
  name             = "external-secrets"
  namespace        = "external-secrets"
  create_namespace = true
  repository       = "https://charts.external-secrets.io"
  chart            = "external-secrets"
}

resource "helm_release" "argocd" {
  name             = "argocd"
  namespace        = "argocd"
  create_namespace = true
  repository       = "https://argoproj.github.io/argo-helm"
  chart            = "argo-cd"
}

# Velero pro MinIO — complementa o PITR do Postgres (banco recupera dado,
# Velero recupera o cluster). Credenciais reais via External Secrets, não aqui.
resource "helm_release" "velero" {
  name             = "velero"
  namespace        = "velero"
  create_namespace = true
  repository       = "https://vmware-tanzu.github.io/helm-charts"
  chart            = "velero"

  set {
    name  = "configuration.backupStorageLocation[0].bucket"
    value = "cluster-backups"
  }
  set {
    name  = "configuration.backupStorageLocation[0].config.s3Url"
    value = "http://minio.dados:9000"
  }
}

# Registry self-hosted com scan (Trivy) e assinatura — o cluster recusa imagem
# não assinada (admission policy). Off por padrão: só quando o GHCR não bastar.
resource "helm_release" "harbor" {
  count            = var.enable_harbor ? 1 : 0
  name             = "harbor"
  namespace        = "harbor"
  create_namespace = true
  repository       = "https://helm.goharbor.io"
  chart            = "harbor"

  set {
    name  = "expose.type"
    value = "clusterIP"
  }
  set {
    name  = "trivy.enabled"
    value = "true"
  }
}
