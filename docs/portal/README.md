# Portal de docs (Docusaurus)

Fonte única dos docs da plataforma — reaproveita `docs/arquitetura.md` e
`docs/governanca/**` (não duplica nada).

```bash
docker compose --profile plataforma-eng up -d docs-portal   # http://localhost:3003
# ou local:
cd docs/portal && npm install && npm start
```
