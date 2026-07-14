variable "kubeconfig" {
  description = "Caminho do kubeconfig do cluster alvo."
  type        = string
  default     = "/etc/rancher/k3s/k3s.yaml"
}

variable "enable_harbor" {
  description = "Sobe o Harbor (registry self-hosted com Trivy embutido). Off por padrão: o CI usa GHCR."
  type        = bool
  default     = false
}

variable "environment" {
  description = "Ambiente lógico (dev/staging/prod) — promoção via pull request, nunca apply manual em prod."
  type        = string
  default     = "dev"
}
