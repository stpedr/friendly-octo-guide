# Motor de query sobre o Big Data Pool: decisão registrada (ADR)

**Status: decidido em 2026-07 — DuckDB sobre o lake particionado que já existe;
Parquet e Trino como otimizações de fase 2, com gatilho de dor.**

## Contexto

O Big Data Pool (MinIO, WORM, linhagem) guardava tudo com integridade, mas
**nada lia** o lake para análise ad-hoc ou reprocessamento histórico. O único
consumidor era a Feature Store. Faltava responder "rode uma query sobre 2 anos de
eventos" sem passar por um serviço .NET — o que limita o estágio 4 do acatech
("por que aconteceu?") à janela quente do Postgres/TSDB.

## Decisão

### 1. DuckDB, não Trino (ainda)

| | DuckDB | Trino |
|---|---|---|
| Operação | binário embutido, zero cluster | coordinator + workers, JVM |
| Custo p/ time de 4 | mínimo | alto (mais um sistema distribuído) |
| Escala | 1 nó, TB-scale por query | many-node, concorrência alta |
| Lê S3/MinIO | sim (httpfs) | sim |

Pela [régua de carga operacional](./carga-operacional.md), **complexidade sem dor
correspondente é custo puro**. Um time de 4 não opera um Trino pra rodar consulta
histórica esporádica. DuckDB lê Parquet/JSON direto do MinIO, roda num nó, e
resolve o caso real. Trino entra **só** se a concorrência de analistas ou o
volume por query passar do que um nó aguenta (fase 2).

### 2. Nenhuma mudança no Data.Archiver na fase 1

O lake **já** é particionado Hive-style pelo `ArchivePartitioner`:

```
{topico}/dt=YYYY-MM-DD/hour=HH/part-NNN-OFFSET.jsonl.gz
```

DuckDB lê **JSONL.gz particionado direto**, com *predicate pushdown* em `dt`/`hour`
(via `hive_partitioning=true`). Ou seja: a capacidade de query nasce **sem tocar
no archiver** — o formato atual já serve. Adicionar Parquet agora seria
otimização antes da dor.

### 3. Parquet é fase 2, com gatilho explícito

Parquet (colunar) ganha de JSONL.gz quando as queries varrem **muitos objetos e
poucas colunas** — aí a compressão colunar e o *column pruning* pagam. O gatilho
pra migrar: query típica lendo > ~dezenas de GB de JSONL ou latência de scan
incomodando. Até lá, JSONL.gz + DuckDB basta.

Quando migrar: o `Data.Archiver` ganha um modo de escrita Parquet (mantendo o
mesmo particionamento e a chave determinística), e o histórico JSONL convive —
DuckDB lê os dois. É trabalho aditivo, não reescrita.

## Como usar (o que já dá pra rodar)

Exemplos executáveis em `deploy/analytics/duckdb/`:
- `setup.sql` — configura httpfs + credenciais do MinIO;
- `exemplos.sql` — queries reais sobre o lake (telemetria, auditoria, contagem
  por dia, Pareto de rejeições).

```bash
# DuckDB local apontando pro MinIO do compose:
duckdb -init deploy/analytics/duckdb/setup.sql < deploy/analytics/duckdb/exemplos.sql
```

## Definição de pronto (issue #3)

- [x] ADR escolhendo o motor (DuckDB vs Trino) com gatilho de dor.
- [x] Query de exemplo sobre o lake documentada e executável.
- [~] "Data.Archiver grava em formato colunar particionado": **decidido adiar** —
  o lake já é particionado (JSONL.gz) e DuckDB o consulta direto; Parquet é fase 2
  com gatilho acima. A capacidade de query — o objetivo da issue — está entregue
  sem o writer Parquet.

## O que falta (fase 2)

- Modo de escrita Parquet no `Data.Archiver` (quando o gatilho de scan aparecer).
- Um catálogo (ex.: DuckDB `ATTACH` ou um Glue-like OSS) se o número de tabelas
  crescer a ponto de valer descoberta automática.
