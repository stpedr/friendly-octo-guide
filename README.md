# plataforma-linha

Monorepo da plataforma (fase 1). Um repositório, um pipeline reutilizável, um serviço por pasta —
com time de 4 pessoas, monorepo elimina o overhead de sincronizar versões entre N repos; se um dia
um serviço precisar de ciclo próprio, ele sai daqui com histórico (`git filter-repo`).

## Layout

```
plataforma-linha/
├── .github/workflows/
│   ├── _dotnet-service.yml      # workflow reutilizável: gate TDD → build → testes+cobertura → imagem
│   └── telemetry-ingest.yml     # instância fina por serviço (path filter)
├── src/
│   └── Telemetry.Ingest/        # 1º serviço: consome Kafka, aplica quality gate, grava Postgres+MinIO
│       ├── Telemetry.Ingest.Domain/    # regra pura, zero dependência de infra — onde o TDD vive
│       ├── Telemetry.Ingest.Worker/    # host: Kafka consumer, OTel, Serilog, health checks
│       └── Telemetry.Ingest.Tests/     # xUnit — espelha a estrutura do Domain
├── schemas/                     # Avro dos tópicos Kafka — ÚNICA parte da PoC que sobrevive aqui
├── deploy/telemetry-ingest/     # manifests K8s (fase 1); PoC roda via docker-compose
├── docker-compose.yml           # dev local: Kafka (KRaft), Postgres, MinIO
└── Directory.Build.props        # nullable, warnings-as-errors, cobertura mínima — vale pra todos
```

## Convenções (não são sugestões)

1. **TDD é gate de CI, não cultura.** PR que altera `src/**` sem alterar o `*.Tests` correspondente
   é reprovado pelo job `tdd-gate`. Cobertura de linha mínima: 80% no projeto Domain.
2. **Domain não referencia infra.** `*.Domain.csproj` não pode referenciar Kafka, Npgsql, nada de IO.
   É isso que mantém os testes rápidos (< 5s a suíte) e o TDD viável.
3. **Todo serviço nasce instrumentado.** OTel (traces+métricas) e Serilog estruturado já vêm no
   template do Worker — serviço sem telemetria não passa no review.
4. **Schema primeiro.** Mudança de contrato começa em `schemas/`, com compatibilidade BACKWARD
   validada no CI antes de qualquer código.

## Rodando local

```bash
docker compose up -d          # Kafka + Postgres + MinIO
dotnet test                   # suíte completa (rápida — só Domain tem lógica)
dotnet run --project src/Telemetry.Ingest/Telemetry.Ingest.Worker
```

> Versões de pacote nos `.csproj` são as vigentes na criação do scaffold — rode
> `dotnet list package --outdated` na primeira semana e fixe as atuais.
