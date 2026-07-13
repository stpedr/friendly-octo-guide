# Multi-tenant: decisão registrada (ADR)

**Status: decidido em 2026-07 — single-tenant por instância, multi-planta por atributo.**

## Contexto

A proposta de arquitetura deixou multi-tenancy como questão em aberto. Os
cenários considerados:

1. **Uma fábrica, várias linhas/plantas** (o caso real hoje);
2. Várias empresas na mesma instância (SaaS).

## Decisão

A plataforma é **single-tenant por instância**: uma empresa = um deploy
(compose ou cluster/namespace próprio). Isolamento entre **plantas/linhas da
mesma empresa** é feito por **atributo ABAC**, que já existe de ponta a ponta:

- O JWT carrega `attr:planta`/`attr:linha`/`attr:turno` (Identity/Keycloak);
- O Gateway e os serviços avaliam `RouteRequirement` com esses atributos
  (`Platform.AccessControl`), e o RAG/Knowledge filtram visibilidade com o
  mesmo mecanismo.

## Por que não SaaS multi-tenant agora

- **Blast radius**: um bug de filtro num sistema com atuador físico não pode
  ter como pior caso "empresa A comandou a linha da empresa B". Isolamento
  por instância elimina essa classe de erro por construção.
- **Custo real**: o stack inteiro roda numa SBC ARM64 — instância por cliente
  é barata; a complexidade de `tenant_id` em toda tabela, tópico, índice
  vetorial e métrica não se paga.
- **LGPD**: controlador/operador por instância simplifica papel e retenção.

## Consequências

- Nenhuma tabela/tópico carrega `tenant_id` — e PR que introduzir um deve
  referenciar a revisão deste ADR.
- Provisionar cliente novo = provisionar instância (compose/Helm já
  parametrizados); o funil de automação disso é trabalho de plataforma, não
  de produto.

## Gatilho de revisão

Rever esta decisão se surgir requisito de >10 instâncias operadas pelo mesmo
time (dor de frota) ou marketplace/self-service de onboarding (dor de venda).
