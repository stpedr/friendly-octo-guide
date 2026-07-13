# Knowledge

Base de conhecimento não-relacional da plataforma: documentos com metadados
JSONB e busca semântica por **pgvector** (índice HNSW, cosseno), exposta por
**GraphQL** (HotChocolate) em `/v1/knowledge/graphql` — atrás do Gateway, com
papel `operador`/`admin` exigido na borda e JWT revalidado aqui dentro.

## Como funciona

- **Ingestão** (`mutation ingestDocument`): o texto é dividido em chunks por
  parágrafo (`DocumentChunker`, determinístico), cada chunk vira embedding e
  documento + chunks entram numa transação só — nunca existe documento
  meio-indexado. Reenviar o mesmo `id` reindexa.
- **Busca** (`query search`): o texto da consulta vira embedding e a
  similaridade é resolvida NO banco. A visibilidade por papel
  (`visibleToRoles`) é filtrada dentro da query — chunk fora do RBAC do
  chamador nem sai do Postgres.
- **Embeddings**: endpoint OpenAI-compatível (`Embeddings:BaseUrl`, ex.: o
  serving da Jetson em `deploy/jetson/`) ou, sem modelo configurado, o
  `HashingEmbedder` local determinístico que mantém o pipeline inteiro
  funcional em dev.

## Configuração

| Chave | Default | Uso |
|---|---|---|
| `ConnectionStrings:Postgres` | localhost/knowledge | banco com a extensão `vector` (imagem `pgvector/pgvector:pg17` nos composes) |
| `Embeddings:BaseUrl` | — (embedder local) | endpoint `/v1/embeddings` OpenAI-compatível |
| `Embeddings:Model` | `nomic-embed-text` | modelo pedido ao endpoint |
| `Embeddings:Dimensions` | `384` | dimensão do índice — trocar exige reindexar |
| `Keycloak:BaseUrl` / `Jwt:SigningKey` | — / dev | mesma validação dual do Gateway |

## Exemplo

```graphql
mutation {
  ingestDocument(title: "Runbook da linha 2",
                 content: "Parada por temperatura alta exige inspeção do termopar...",
                 visibleToRoles: ["operador"])
}

query {
  search(query: "o que fazer em parada por temperatura?", limit: 5) {
    title content score
  }
}
```

Consumidores hoje: a ferramenta MCP `buscar_conhecimento` do Chatbot (repassa
o token do usuário — a visibilidade é a DELE) e o front via Gateway.
