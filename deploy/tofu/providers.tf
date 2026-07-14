# OpenTofu (fork Apache-2.0 do Terraform): a INFRA como código. O GitOps (ArgoCD)
# cobre as APLICAÇÕES; isto cobre o que está ABAIXO delas — add-ons de cluster,
# namespaces, registry. Rodar: tofu init && tofu plan && tofu apply.
terraform {
  required_version = ">= 1.7"
  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.31"
    }
    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.15"
    }
  }
}

# Aponta pro kubeconfig do k3s (deploy/cluster/bootstrap-server.sh gera em
# /etc/rancher/k3s/k3s.yaml). Sobrescreva via -var pra outro cluster.
provider "kubernetes" {
  config_path = var.kubeconfig
}

provider "helm" {
  kubernetes {
    config_path = var.kubeconfig
  }
}
