# Segurança de runtime

Detecção em runtime (Falco) e correlação de eventos de segurança — o funil
**separado** da observabilidade operacional. A racional completa (por que
separar, retenção, resposta a incidente) está em
[`docs/governanca/seguranca-runtime-siem.md`](../../docs/governanca/seguranca-runtime-siem.md).

## Arquivos

- `falco/rules-plataforma.yaml` — regras específicas da plataforma (shell em pod
  de serviço, saída de rede do edge OT, escrita em binário, leitura de segredo).
  **Fonte de verdade das regras.**
- `falco/daemonset.yaml` — Falco como DaemonSet (fase 2, K8s). O ConfigMap de
  regras é gerado a partir de `rules-plataforma.yaml` pelo GitOps
  (`configMapGenerator`), não duplicado à mão.

## Fases

- **Fase 1 (Pi + compose)**: Falco host-level com o subconjunto de regras
  aplicável fora de K8s. As regras `pod_de_servico`/`pod_voltado_ao_ot` usam
  labels de K8s — na fase 1 o equivalente é filtro por nome de container.
- **Fase 2 (K8s)**: este DaemonSet + namespace `seguranca` (adicionar em
  `deploy/namespaces.yaml`) + pipeline `seguranca` no OTel Collector com destino
  WORM no MinIO.

Adotar um SIEM OSS dedicado (ex.: Wazuh) é decisão de fase 2, só se o volume
justificar — ver a régua do [doc de carga operacional](../../docs/governanca/carga-operacional.md).
