# Classificação de dados, PII e LGPD

Regra de bolso: **telemetria de máquina não é dado pessoal; tudo que identifica
gente é.** A plataforma nasce com pouquíssima PII de propósito — o que não se
coleta não vaza, não expira e não gera pedido de titular.

## Inventário e classificação

| Dado | Onde vive | Classe | Base legal (LGPD) | Retenção |
|---|---|---|---|---|
| Nome de usuário, hash de senha, segredo TOTP | Identity/Keycloak (Postgres) | **PII — credencial** | Execução de contrato (art. 7º V) | Enquanto a conta existir + 30 dias |
| Papéis e atributos (planta/linha/turno) | Identity/Keycloak | **PII — vínculo funcional** | Legítimo interesse (art. 7º IX) | Igual à conta |
| Logs de acesso (sub do JWT, IP, rota, decisão RBAC) | Loki, via OTel | **PII — registro de acesso** | Obrigação legal / segurança (art. 7º II) | 6 meses (Marco Civil, art. 15) e descarte |
| Conversas do chatbot (prompt + resposta + trace) | Tempo/Loki | **PII — conteúdo** | Consentimento no primeiro uso | 90 dias e descarte |
| Telemetria de sensor (valor, timestamp, sensor-id) | Kafka, Postgres, lake (MinIO) | Não pessoal | — | Indefinida (lake) / 13 meses (Postgres quente) |
| Ordens de produção | Core.Execution (Postgres) | Não pessoal (sem operador nominal) | — | 5 anos (rastreabilidade industrial) |
| Beacon de RUM (rota, status, duração) | VictoriaMetrics | Não pessoal (sem id de usuário — decisão de projeto) | — | 13 meses |

Decisões de projeto que sustentam a tabela:

- O beacon de RUM **não carrega** identificador de usuário nem IP no corpo —
  só rota/status/duração (validado em `RumBeacon.TryParse`).
- Ordem de produção não guarda quem operou; a autoria fica no log de acesso
  (que tem retenção curta), não no dado de negócio (que dura anos).
- O lake (`Data.Archiver`) só recebe o tópico de telemetria — PII não passa
  por lá por construção. Se um dia passar, entra particionada e criptografada
  por chave própria pra permitir esquecimento seletivo.

## Direitos do titular

Acesso/correção: via Identity/Keycloak (self-service). Eliminação: apagar a
conta remove credenciais na hora; logs de acesso expiram sozinhos pela
retenção. Portabilidade não se aplica (não há dado de titular além do
cadastro). Encarregado (DPO): o dono da instância — isto é uma plataforma
self-hosted; quem opera responde.

## Revisão

Este inventário é revisado a cada serviço novo que persista dado. PR que cria
tabela/tópico/bucket novo DEVE atualizar esta tabela — é item de checklist de
review, não boa vontade.
