-- Configura o DuckDB pra ler o Big Data Pool (MinIO/S3) direto.
-- Uso: duckdb -init deploy/analytics/duckdb/setup.sql
--
-- httpfs dá acesso a s3://; as credenciais/endpoint batem com o MinIO do
-- docker-compose.yml (troque por Secrets/OpenBao fora de dev). Em produção,
-- aponte pro endpoint do MinIO do cluster e use uma credencial read-only.

INSTALL httpfs;
LOAD httpfs;

-- Endpoint e credenciais do MinIO local (compose). Path-style: MinIO não faz
-- virtual-host buckets.
SET s3_endpoint = 'localhost:9000';
SET s3_url_style = 'path';
SET s3_use_ssl = false;
SET s3_access_key_id = 'msuchoa';
SET s3_secret_access_key = 'w1ntersun';

-- Dica: com hive_partitioning, filtros em dt=/hour= viram predicate pushdown —
-- a query só abre os objetos das partições que interessam.
