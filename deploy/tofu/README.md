# Infra como código (OpenTofu)

O GitOps (ArgoCD) aplica as **aplicações**; o OpenTofu provisiona a **infra
abaixo delas** — add-ons de cluster (KEDA, External Secrets, ArgoCD, Velero) e,
opcionalmente, Harbor. É a alternativa **declarativa** ao
`deploy/cluster/bootstrap-server.sh` (imperativo). Use um OU outro no mesmo cluster.

```bash
cd deploy/tofu
tofu init
tofu plan  -var environment=dev
tofu apply -var environment=dev
# Harbor (registry próprio, quando o GHCR não bastar):
tofu apply -var enable_harbor=true
```

## Ambientes

dev / staging / prod — promoção via **pull request** (muda `-var environment`
e os values do ambiente), nunca `apply` manual em prod. O state fica num backend
remoto (S3/MinIO) em uso real; aqui o default é local pra bootstrap.

## Harbor + supply chain

Com `enable_harbor=true`, o Harbor sobe com Trivy embutido e o cluster passa a
recusar imagem não assinada (admission policy). O CI então troca o push do GHCR
pelo Harbor (login + prefixo de imagem) — ver `.github/workflows/_dotnet-service.yml`.
