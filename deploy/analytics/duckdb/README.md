# Query analítica sobre o lake — DuckDB

Motor de query sobre o Big Data Pool (MinIO), lendo o JSONL.gz particionado que o
`Data.Archiver` grava. Decisão e gatilho em
[`docs/governanca/motor-query-lake.md`](../../../docs/governanca/motor-query-lake.md).

## Por que DuckDB

Um binário, zero cluster — o custo operacional certo pra um time de 4, contra o
Trino (coordinator + workers). DuckDB lê o lake direto do MinIO via `httpfs`, com
predicate pushdown nas partições `dt=`/`hour=`. Trino só entra se a concorrência
ou o volume por query passar do que um nó aguenta (fase 2).

## Uso

```bash
# precisa do duckdb CLI instalado (binário único, sem dependências)
duckdb -init setup.sql < exemplos.sql
```

- `setup.sql` — httpfs + endpoint/credenciais do MinIO (dev = compose; em prod,
  endpoint do cluster + credencial read-only via OpenBao).
- `exemplos.sql` — leituras por dia, agregação por sensor, malformados, e a
  trilha de auditoria do bucket WORM.

## Formato

Fase 1 lê **JSONL.gz** (o que o archiver já grava). **Parquet** é otimização de
fase 2 — ver o gatilho no ADR. Quando entrar, as mesmas queries funcionam
trocando `read_json_auto` por `read_parquet`; DuckDB lê os dois formatos e o
histórico convive.
