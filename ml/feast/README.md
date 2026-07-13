# Feature store (Feast)

Fecha o ciclo do subsistema preditivo: **as features de treino e as de
inferência são a mesma definição** (`features.py`), com offline store no
Postgres (leituras aceitas) e online store no Valkey.

```bash
pip install 'feast[postgres,redis]'
cd ml/feast
feast apply                                  # registra entidades/views
feast materialize-incremental $(date -u +%Y-%m-%dT%H:%M:%S)   # carrega o online store
```

Uso no treino (ponto-no-tempo correto, sem vazamento de futuro):

```python
store.get_historical_features(entity_df=df_rotulos,
    features=["estatisticas_sensor_1h:media_1h", "estatisticas_sensor_1h:desvio_1h"])
```

Uso na inferência (Valkey, ~ms):

```python
store.get_online_features(features=[...], entity_rows=[{"sensor_id": "temp-forno-01"}])
```

Quando entra de verdade: no momento em que houver um segundo consumidor das
mesmas features (hoje o EWMA online do Predictive basta — ver ADR no
docs/arquitetura.md). A recalibração registrada no MLflow + estas definições
são o pré-requisito; o custo marginal de adotar já foi pago aqui.
