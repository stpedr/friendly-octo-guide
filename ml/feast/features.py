"""Definições de features do preditivo — versionadas junto com o código.

A regra que o Feast garante: treino e inferência leem a MESMA definição.
O que o Predictive calcula online (EWMA/z-score) é decisão por evento; estas
features são as agregações usadas pra treinar/recalibrar modelos (ver
docs/governanca/avaliacao-ia.md — recalibração vira run no MLflow).
"""

from datetime import timedelta

from feast import Entity, FeatureView, Field, PushSource
from feast.infra.offline_stores.contrib.postgres_offline_store.postgres_source import (
    PostgreSQLSource,
)
from feast.types import Float64, Int64

# Sensor é A entidade da plataforma: tudo que o preditivo aprende é por sensor.
sensor = Entity(name="sensor", join_keys=["sensor_id"])

# Fonte offline: a tabela de leituras aceitas pelo quality gate (Telemetry.Ingest).
leituras = PostgreSQLSource(
    name="leituras_aceitas",
    query="""
        SELECT
            sensor_id,
            measured_at AS event_timestamp,
            AVG(value) OVER w AS media_1h,
            STDDEV_SAMP(value) OVER w AS desvio_1h,
            COUNT(*) OVER w AS leituras_1h
        FROM readings
        WINDOW w AS (
            PARTITION BY sensor_id
            ORDER BY measured_at
            RANGE BETWEEN INTERVAL '1 hour' PRECEDING AND CURRENT ROW
        )
    """,
    timestamp_field="event_timestamp",
)

estatisticas_sensor = FeatureView(
    name="estatisticas_sensor_1h",
    entities=[sensor],
    ttl=timedelta(hours=2),  # feature mais velha que isso não serve inferência
    schema=[
        Field(name="media_1h", dtype=Float64),
        Field(name="desvio_1h", dtype=Float64),
        Field(name="leituras_1h", dtype=Int64),
    ],
    online=True,
    source=leituras,
)

# Push source: quando o scoring quiser materializar direto (sem esperar o
# batch), empurra pelo SDK — mesmo nome, mesma entidade, zero skew.
leituras_push = PushSource(name="leituras_push", batch_source=leituras)
