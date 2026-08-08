# Escopo do MVP

> Este documento existe para evitar *scope creep*. Em um desafio técnico, a
> complexidade deve estar em arquitetura, resiliência, consistência, testes,
> qualidade de código e performance — não na quantidade de funcionalidades.

## 1. Fluxo funcional do MVP

```
CASH FLOW
    │
Registrar lançamento
    │
    ├── Crédito
    └── Débito
    │
    ▼
Persistir lançamento  ──┐ (mesma transação)
    │                   │
    ▼                   │
Gravar evento no Outbox ┘
    │
    ▼
Publicar evento na fila
    │
    ▼
Processar consolidação
    │
    ▼
Atualizar saldo diário
    │
    ▼
Consultar saldo diário
```

## 2. Casos de uso

| ID | Caso de uso | Serviço | Prioridade | Requisitos |
|----|-------------|---------|------------|------------|
| UC-01 | `RegisterTransaction` | Cash Flow | Obrigatório | RF-001, RF-002 |
| UC-02 | `GetDailyBalance` | Consolidation | Obrigatório | RF-005, RF-006 |
| UC-03 | `ListTransactions` | Cash Flow | MVP (apoio) | RF-003 |
| UC-06 | `GetTransaction` | Cash Flow | MVP (apoio) | RF-003 |
| UC-04 | `ConsolidateTransaction` (worker) | Consolidation | Obrigatório | RF-004, RNF-008 |
| UC-05 | `PublishPendingOutboxMessages` (worker) | Cash Flow | Obrigatório | RNF-007 |

UC-04 e UC-05 não são disparados por usuário, mas são casos de uso de aplicação
com regra própria e testes próprios.

UC-06 entrou na etapa 4, ao definir o contrato: `POST /transactions` responde
`201` com header `Location`, e um `Location` sem recurso de destino seria um
contrato quebrado. É o menor endpoint do sistema — uma busca por chave primária —
e o repositório já precisa da operação.

## 3. Dentro do escopo

- Registro de lançamento (crédito / débito) com validação de domínio
- Listagem de lançamentos com paginação por cursor e filtro por período
  ([ADR-014](./decisions/ADR-014-cursor-pagination.md)), e consulta individual por id
- Publicação confiável de eventos via Transactional Outbox
- Consumo assíncrono e consolidação diária idempotente
- Consulta de saldo consolidado por data
- Testes unitários de domínio e aplicação + testes de integração dos fluxos
- Docker Compose para subir todo o ambiente localmente
- Script de carga k6 validando 50 req/s
- Logs estruturados e health checks
- Pipeline de CI executando build e testes em todo Pull Request
- Documentação: README, arquitetura, ADRs, diagramas

## 4. Fora do escopo (e por quê)

| Funcionalidade | Decisão | Motivo |
|----------------|---------|--------|
| Autenticação / login | Fora | Não solicitado; não agrega ao que está sendo avaliado |
| Cadastro de usuários | Fora | Não solicitado |
| Múltiplos lojistas (multi-tenant) | Fora | Não solicitado; adicionaria `TenantId` a todo o modelo |
| Categorias financeiras | Fora | Não solicitado |
| Contas bancárias | Fora | Não solicitado |
| Cartões | Fora | Não solicitado |
| Importação de arquivos (OFX/CSV) | Fora | Não solicitado |
| Dashboard / frontend complexo | Fora | O desafio é de backend |
| Relatórios financeiros avançados | Fora | Apenas o saldo diário foi pedido |
| Permissões e roles | Fora | Consequência da ausência de autenticação |
| Notificações | Fora | Não solicitado |
| Edição e exclusão de lançamentos | Fora | Lançamentos imutáveis no MVP (premissa P-05) |
| Estorno / lançamento compensatório | Fora | Modelável como novo lançamento de tipo oposto, se necessário |
| Saldo acumulado (running balance) | Fora | O enunciado pede saldo **diário** |
| Múltiplas moedas | Fora | BRL única (premissa P-03) |
| Kubernetes / deploy em nuvem | Fora | Docker Compose atende à reprodutibilidade local exigida |

> Itens fora de escopo que consideramos arquiteturalmente interessantes são
> registrados em "Melhorias futuras" no README, conforme o próprio enunciado
> sugere.

## 5. Definition of Done do projeto

Um item só é considerado pronto quando:

- [ ] Existe teste automatizado escrito **antes** da implementação (TDD)
- [ ] A suíte completa passa (`dotnet test`) e o CI está verde no Pull Request
- [ ] O código respeita as fronteiras de camada da Clean Architecture
- [ ] Não há regra de negócio fora de `Domain` ou `Application`
- [ ] Decisões **estruturais** novas viraram ADR (critério em
      [`decisions/README.md`](./decisions/README.md#quando-criar-uma-nova-adr))
- [ ] O ambiente sobe com um único `docker compose up`
- [ ] O README reflete o estado real do projeto
