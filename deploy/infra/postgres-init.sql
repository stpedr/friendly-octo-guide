-- Roda só no PRIMEIRO boot do volume (contrato do docker-entrypoint-initdb.d).
-- Um cluster Postgres, vários bancos: isola dados por domínio sem operar N instâncias.
CREATE DATABASE knowledge;
CREATE DATABASE unleash;

-- Usuário de replicação da réplica de leitura (perfil ha).
CREATE ROLE replicator WITH REPLICATION LOGIN PASSWORD 'replicator-dev';
