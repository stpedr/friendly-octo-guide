-- Queries de exemplo sobre o Big Data Pool (MinIO), lidas direto do JSONL.gz
-- particionado Hive-style que o Data.Archiver grava. Rode após o setup.sql.
--
--   duckdb -init deploy/analytics/duckdb/setup.sql < deploy/analytics/duckdb/exemplos.sql
--
-- Bucket de telemetria: linha-lake ; de auditoria: linha-audit (WORM).
-- O glob **/*.jsonl.gz varre todas as partições; o filtro em dt/hour poda.

-- 1) Quantas leituras por dia (predicate pushdown na partição dt=).
SELECT dt, count(*) AS leituras
FROM read_json_auto(
       's3://linha-lake/linha.telemetria.v1/**/*.jsonl.gz',
       hive_partitioning = true)
WHERE dt >= '2026-07-01'
GROUP BY dt
ORDER BY dt;

-- 2) Valor médio/máx por sensor num intervalo (poda por dt e hour).
SELECT sensorId,
       count(*)       AS n,
       avg(value)     AS media,
       max(value)     AS maximo
FROM read_json_auto(
       's3://linha-lake/linha.telemetria.v1/**/*.jsonl.gz',
       hive_partitioning = true)
WHERE dt = '2026-07-14' AND hour BETWEEN '08' AND '17'
GROUP BY sensorId
ORDER BY n DESC;

-- 3) Payloads que nem decodificaram (o archiver grava o cru em base64 com
--    malformed=true) — quantos por dia, pra fechar o loop com a quarentena.
SELECT dt, count(*) AS malformados
FROM read_json_auto(
       's3://linha-lake/linha.telemetria.v1/**/*.jsonl.gz',
       hive_partitioning = true)
WHERE malformed = true
GROUP BY dt
ORDER BY dt;

-- 4) Trilha de auditoria administrativa (bucket WORM): quem mudou permissão de
--    quem, por ação. before/after já vêm redigidos na origem.
SELECT action,
       actor,
       targetId,
       count(*) AS eventos
FROM read_json_auto(
       's3://linha-audit/auditoria.admin.v1/**/*.jsonl.gz',
       hive_partitioning = true)
GROUP BY action, actor, targetId
ORDER BY eventos DESC;
