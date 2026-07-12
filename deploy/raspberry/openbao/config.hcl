storage "file" {
  path = "/openbao/data"
}

listener "tcp" {
  address     = "0.0.0.0:8200"
  tls_disable = true # rede interna do compose; sem TLS entre serviços ainda (mesmo estágio do resto do stack)
}

disable_mlock = true # Pi sem CAP_IPC_LOCK garantido fora de root; sem isso o processo nem sobe
api_addr      = "http://openbao:8200"
