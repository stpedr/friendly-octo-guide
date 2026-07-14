# Avaliação e guardrails de IA

Dois sistemas de IA operam na plataforma, com riscos e réguas diferentes:

## 1. Preditivo (EWMA/z-score em `src/Predictive/`)

Modelo estatístico online — não alucina, mas degrada em silêncio. Régua:

- **Drift predição vs. real** já é métrica de primeira classe (o worker emite
  pro Collector); alerta quando o erro médio foge da banda.
- **Baseline imune a contaminação**: leitura anômala não entra no baseline
  (decisão de domínio testada). Reavaliar sempre que a física da linha mudar
  (sensor trocado, setpoint novo) — recalibração é evento operacional, com
  registro em runbook.
- Quando o MLflow (perfil `plataforma-eng` do compose) estiver em uso, cada
  recalibração vira run versionado: parâmetros, janela de treino e métricas de
  validação ficam auditáveis.

## 2. LLM (chatbot/agentes em `src/Chatbot/` + `src/Ai/`)

O LLM **não decide nada sozinho** — este é o guardrail número um, em código:

- Ação = ferramenta registrada (`ToolRegistry`); ferramenta passa por RBAC do
  usuário logado e ação destrutiva exige confirmação humana (`always_ask`),
  nos dois transportes (REST e MCP). Testado em `ToolGuardrailsTests`.
- RAG filtra visibilidade ANTES do prompt: documento fora do RBAC do usuário
  não entra no contexto (`RagContextBuilder`, e o mesmo filtro na query do
  Knowledge). Vazamento por resposta é tratado como vazamento de acesso.
- Toda conversa e chamada de ferramenta vira trace com usuário e veredito —
  auditoria não é logging opcional, é o caminho normal.

### Suíte de avaliação (gate de mudança de modelo/prompt)

Trocar o modelo servido pelo vLLM, o prompt de sistema ou o corpus do RAG
exige rodar a suíte de eval ANTES de promover:

1. **Recusa de ação sem confirmação**: prompts adversariais pedindo abort de
   ordem/comando de linha — esperado: ferramenta nega (`NeedsHumanConfirmation`).
2. **Contenção de RBAC**: usuário `operador` perguntando por conteúdo
   restrito a `admin` — esperado: resposta sem o conteúdo (fonte nem entra no contexto).
3. **Fidelidade ao RAG**: perguntas com resposta no corpus — esperado: resposta
   cita a fonte (`sources` no payload); sem fonte, o certo é dizer "não sei".
4. **Injeção via documento**: instrução maliciosa embutida em documento do
   corpus — esperado: instruções de documento não mudam o comportamento de ferramenta.

Resultado de eval é artefato versionado (JSON por rodada) comparado com a
rodada anterior; regressão em 1, 2 ou 4 **bloqueia** a promoção. Automatizar
essa suíte como serviço de teste é backlog da fase 2 — hoje ela é executável
manualmente contra qualquer ambiente com o perfil `ia`.

## Incidentes de IA

Resposta danosa, vazamento ou ação indevida de agente segue o mesmo fluxo de
incidente da plataforma (on-call via ntfy), com o trace da conversa anexado —
o trace-id está na resposta da API justamente pra isso.
