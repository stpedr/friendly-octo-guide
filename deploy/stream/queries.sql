-- Stream processing versionado (ksqlDB): janelas e agregações que os workers
-- .NET não fazem (eles decidem por-evento; aqui é o agregado no tempo).
--
-- Aplicar:  docker compose --profile stream up -d
--           docker exec -i <ksqldb> ksql http://localhost:8088 < deploy/stream/queries.sql
--
-- Escopo: tópicos de CONTROLE (JSON). O tópico de telemetria (Avro binário do
-- codec próprio) entra quando o producer migrar pro wire-format do registry —
-- decisão registrada em docs/arquitetura.md.

SET 'auto.offset.reset' = 'earliest';

-- ── Alertas (linha.alertas.v1, key = sensorId) ─────────────────────────────
CREATE STREAM IF NOT EXISTS alertas (
    sensor_id VARCHAR KEY,
    id VARCHAR,
    title VARCHAR,
    body VARCHAR,
    severity VARCHAR,
    raisedAt VARCHAR
) WITH (KAFKA_TOPIC = 'linha.alertas.v1', VALUE_FORMAT = 'JSON');

-- Tempestade de alertas: 5+ alertas do MESMO sensor em 10 minutos vira um
-- evento próprio — é sinal de sensor doente ou processo instável, e o on-call
-- precisa de UM aviso agregado, não de 50 pushes.
CREATE TABLE IF NOT EXISTS alertas_tempestade
    WITH (KAFKA_TOPIC = 'linha.alertas.tempestade.v1', VALUE_FORMAT = 'JSON') AS
SELECT
    sensor_id,
    COUNT(*) AS total_na_janela,
    LATEST_BY_OFFSET(severity) AS ultima_severidade,
    WINDOWSTART AS janela_inicio
FROM alertas
WINDOW TUMBLING (SIZE 10 MINUTES)
GROUP BY sensor_id
HAVING COUNT(*) >= 5
EMIT CHANGES;

-- ── Eventos de ordem (core.eventos.v1) ─────────────────────────────────────
CREATE STREAM IF NOT EXISTS eventos_core (
    orderId VARCHAR,
    type VARCHAR,
    occurredAt VARCHAR,
    line VARCHAR,
    state VARCHAR
) WITH (KAFKA_TOPIC = 'core.eventos.v1', VALUE_FORMAT = 'JSON');

-- Throughput por linha/hora: quantas ordens concluíram — o número que o
-- painel de gestão quer, calculado no stream em vez de query no Postgres quente.
CREATE TABLE IF NOT EXISTS throughput_por_linha
    WITH (KAFKA_TOPIC = 'linha.throughput.v1', VALUE_FORMAT = 'JSON') AS
SELECT
    line,
    COUNT(*) AS ordens_concluidas,
    WINDOWSTART AS janela_inicio
FROM eventos_core
WINDOW TUMBLING (SIZE 1 HOUR)
WHERE state = 'Completed'
GROUP BY line
EMIT CHANGES;
