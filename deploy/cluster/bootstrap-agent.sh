#!/bin/bash
# Bootstrap de um nó AGENT do k3s (rodar na Pi e na Jetson, como root).
# Exige K3S_URL (https://<ip-do-server>:6443) e K3S_TOKEN (impresso pelo
# bootstrap-server.sh). Idempotente.
set -euo pipefail

: "${K3S_URL:?defina K3S_URL=https://<ip-do-server>:6443}"
: "${K3S_TOKEN:?defina K3S_TOKEN=<token do server>}"

# cgroups de memória: a Pi vem com eles desligados e o kubelet não sobe sem.
if grep -qi raspbian /etc/os-release 2>/dev/null || [ -f /boot/firmware/cmdline.txt ]; then
  CMDLINE=/boot/firmware/cmdline.txt
  [ -f "$CMDLINE" ] || CMDLINE=/boot/cmdline.txt
  if ! grep -q cgroup_memory=1 "$CMDLINE"; then
    sed -i 's/$/ cgroup_memory=1 cgroup_enable=memory/' "$CMDLINE"
    echo "cgroups habilitados em $CMDLINE — REINICIE e rode este script de novo."
    exit 0
  fi
fi

curl -sfL https://get.k3s.io | K3S_URL="$K3S_URL" K3S_TOKEN="$K3S_TOKEN" sh -

echo "agent no ar. No server, confira com: kubectl get nodes"
echo "se este nó for a Jetson: kubectl label node $(hostname) gpu=nvidia"
