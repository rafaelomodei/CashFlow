# Progresso de Implementação

> Backlog executável do projeto. Enquanto [`roadmap.md`](./roadmap.md) responde
> *"quais são as grandes fases e por que nesta ordem"*, este documento responde
> *"qual é exatamente o próximo item a fazer"*.
>
> Regra de uso: **este arquivo é atualizado no mesmo Pull Request que entrega o
> item**. Checkbox marcado sem entrega correspondente é ruído, não progresso.

**Etapa atual: 4 — Contratos de API e eventos**

## Progresso macro

```
[x] Etapa 1  Entendimento do desafio
[x] Etapa 2  Mapeamento de requisitos
[x] Etapa 3  Decisões arquiteturais (ADRs)
[~] Etapa 4  Contratos de API e eventos
[ ] Etapa 5  Esqueleto da solução, ambiente e CI
[ ] Etapa 6  Domínio (TDD)
[ ] Etapa 7  Casos de uso (TDD)
[ ] Etapa 8  Infraestrutura de lançamentos
[ ] Etapa 9  Mensageria e outbox
[ ] Etapa 10 Consolidação e idempotência
[ ] Etapa 11 APIs HTTP
[ ] Etapa 12 Resiliência e observabilidade
[ ] Etapa 13 Testes de carga
[ ] Etapa 14 README final e revisão
```

Legenda: `[x]` concluída · `[~]` em andamento · `[ ]` pendente

---

## Etapa 1 — Entendimento do desafio ✅

- [x] Ler o enunciado do desafio
- [x] Versionar o enunciado em [`challenge/`](./challenge/)
- [x] Identificar requisitos explícitos
- [x] Identificar ambiguidades do enunciado
- [x] Registrar as ambiguidades como premissas (P-01 a P-07)

## Etapa 2 — Mapeamento de requisitos ✅

- [x] Requisitos funcionais (RF-001 a RF-006)
- [x] Requisitos não funcionais com critério de aceite (RNF-001 a RNF-014)
- [x] Restrições técnicas do enunciado (RT-001 a RT-008)
- [x] Restrições auto-impostas (RT-101 a RT-104)
- [x] Regras de negócio (RN-001 a RN-004)
- [x] Matriz de rastreabilidade requisito → decisão
- [x] Escopo do MVP fechado em [`scope.md`](./scope.md)
- [x] Lista explícita do que ficou fora de escopo, com justificativa

## Etapa 3 — Decisões arquiteturais ✅

- [x] ADR-001 Clean Architecture com SOLID
- [x] ADR-002 Decomposição em dois serviços
- [x] ADR-003 Mensageria (RabbitMQ)
- [x] ADR-004 Transactional Outbox
- [x] ADR-005 Banco de dados
- [x] ADR-006 Consistência eventual
- [x] ADR-007 Idempotência no consumidor
- [x] ADR-008 TDD
- [x] ADR-009 Containers
- [x] ADR-010 Validação de performance
- [x] ADR-011 Observabilidade
- [x] ADR-012 Stack tecnológica
- [x] ADR-013 Representação monetária
- [x] Visão arquitetural e diagramas em [`architecture.md`](./architecture.md)
- [x] Estratégia de testes em [`testing-strategy.md`](./testing-strategy.md)
- [x] Roadmap de execução em [`roadmap.md`](./roadmap.md)

---

## Etapa 4 — Contratos de API e eventos 🚧

> Saída: `docs/api-contracts.md`. Nenhum código nesta etapa — o contrato é a
> fronteira entre os dois contextos e mudá-lo depois custa retrabalho dos dois lados.

### Cash Flow API — `POST /transactions`

- [ ] Definir rota e verbo
- [ ] Definir request DTO
- [ ] Definir campos obrigatórios e opcionais
- [ ] Definir representação de `CREDIT` / `DEBIT` (RF-002, RN-002)
- [ ] Definir representação monetária no contrato (ADR-013, RN-001)
- [ ] Definir formato de `occurredAt` (ISO 8601 / UTC, premissa P-04)
- [ ] Definir política para lançamento retroativo (premissa P-06)
- [ ] Definir resposta `201 Created`
- [ ] Definir header `Location`
- [ ] Definir response DTO do recurso criado
- [ ] Definir erro para `amount` inválido (RN-001)
- [ ] Definir erro para `type` inválido (RN-002)
- [ ] Definir erro para `occurredAt` inválido
- [ ] Definir limite de tamanho de `description`
- [ ] Criar exemplo de request
- [ ] Criar exemplo de response

### Cash Flow API — `GET /transactions`

- [ ] Definir rota
- [ ] Definir estratégia de paginação
- [ ] Definir parâmetros de paginação e seus limites
- [ ] Definir filtro `startDate`
- [ ] Definir filtro `endDate`
- [ ] Definir ordenação padrão
- [ ] Definir response DTO da coleção
- [ ] Definir metadados de paginação na resposta
- [ ] Definir comportamento sem registros
- [ ] Definir erro para intervalo de datas inválido
- [ ] Criar exemplos de request e response

### Consolidation API — `GET /daily-balances/{date}`

- [ ] Definir formato da data no path
- [ ] Definir response DTO
- [ ] Definir `totalCredits`
- [ ] Definir `totalDebits`
- [ ] Definir `balance` (RF-004)
- [ ] Definir `updatedAt` como evidência de consistência eventual (ADR-006)
- [ ] Definir comportamento para dia sem lançamentos
- [ ] Definir erro para data em formato inválido
- [ ] Criar exemplos de request e response

### Erros HTTP

- [ ] Adotar Problem Details (RFC 7807)
- [ ] Definir estrutura comum de erro
- [ ] Definir `400 Bad Request`
- [ ] Definir formato dos erros de validação campo a campo
- [ ] Definir `404 Not Found` e quando ele se aplica
- [ ] Definir `500 Internal Server Error`
- [ ] Definir `correlationId` no corpo do erro (ADR-011)
- [ ] Criar exemplo de cada formato de erro

### Evento `TransactionRegistered`

- [ ] Confirmar o nome do evento
- [ ] Definir versionamento do contrato
- [ ] Definir `eventId` (base da idempotência — ADR-007)
- [ ] Definir `eventType`
- [ ] Definir `occurredAt` do envelope (instante da emissão)
- [ ] Definir `correlationId` no envelope (ADR-011)
- [ ] Definir `data.transactionId`
- [ ] Definir `data.occurredAt` (dia da consolidação — RN-004)
- [ ] Definir `data.amount`
- [ ] Definir `data.type`
- [ ] Definir exemplo JSON completo
- [ ] Definir política de compatibilidade e evolução do schema
- [ ] Definir routing key / nome do exchange e da fila (ADR-003)

### Especificação e documentação

- [ ] Criar `docs/api-contracts.md`
- [ ] Definir como a especificação OpenAPI será gerada e publicada
- [ ] Revisar contratos contra [`requirements.md`](./requirements.md)
- [ ] Revisar contratos contra as ADRs
- [ ] Revisar contratos contra [`scope.md`](./scope.md)
- [ ] Atualizar a seção "API" do README
- [ ] Atualizar o índice em [`docs/README.md`](./README.md)
- [ ] Atualizar este arquivo

### Definition of Done da etapa

- [ ] Todos os endpoints do MVP possuem contrato completo
- [ ] Todos os status HTTP possíveis estão definidos
- [ ] O evento possui schema estável e versionado
- [ ] Ambiguidades de contrato foram eliminadas
- [ ] Nenhuma regra de negócio nova foi inventada nesta etapa

---

## Etapa 5 — Esqueleto da solução, ambiente e CI

### Solution e convenções

- [ ] Criar `CashFlow.sln`
- [ ] Criar `global.json` fixando .NET 10
- [ ] Criar `Directory.Build.props`
- [ ] Configurar `TreatWarningsAsErrors`
- [ ] Configurar `Nullable` e `ImplicitUsings`
- [ ] Criar `.editorconfig`
- [ ] Atualizar `.gitignore` para artefatos .NET

### Projetos — Cash Flow

- [ ] `src/CashFlow.Domain`
- [ ] `src/CashFlow.Application`
- [ ] `src/CashFlow.Infrastructure`
- [ ] `src/CashFlow.Api`

### Projetos — Consolidation

- [ ] `src/Consolidation.Domain`
- [ ] `src/Consolidation.Application`
- [ ] `src/Consolidation.Infrastructure`
- [ ] `src/Consolidation.Api`
- [ ] `src/Consolidation.Worker`

### Projetos — Shared

- [ ] `src/Shared.Contracts` (somente contratos de evento — sem regra de negócio)

### Projetos de teste

- [ ] `tests/CashFlow.Domain.UnitTests`
- [ ] `tests/CashFlow.Application.UnitTests`
- [ ] `tests/Consolidation.Domain.UnitTests`
- [ ] `tests/Consolidation.Application.UnitTests`
- [ ] `tests/CashFlow.IntegrationTests`
- [ ] `tests/Consolidation.IntegrationTests`
- [ ] `tests/ArchitectureTests`
- [ ] Configurar xUnit, FluentAssertions e NSubstitute (ADR-008)
- [ ] Configurar categorias/traits separando unitários de integração

### Fronteiras arquiteturais

- [ ] Configurar as referências permitidas entre projetos
- [ ] Escrever o primeiro Architecture Test
- [ ] Teste: `CashFlow.Domain` não referencia `Infrastructure`
- [ ] Teste: `CashFlow.Application` não referencia `Infrastructure`
- [ ] Teste: `Consolidation.Domain` não referencia `Infrastructure`
- [ ] Teste: `Consolidation.Application` não referencia `Infrastructure`
- [ ] Teste: nenhum contexto referencia o outro além de `Shared.Contracts`

### Ambiente Docker

- [ ] `docker-compose.yml`
- [ ] PostgreSQL `cashflow_db`
- [ ] PostgreSQL `consolidation_db`
- [ ] RabbitMQ com management plugin
- [ ] Health checks dos serviços de infraestrutura
- [ ] Volumes nomeados para persistência
- [ ] `.env.example`
- [ ] `Dockerfile` da Cash Flow API
- [ ] `Dockerfile` da Consolidation API
- [ ] `Dockerfile` do Consolidation Worker
- [ ] Validar consumo de recursos do ambiente completo (risco registrado no roadmap)

### Integração contínua

- [ ] Criar `.github/workflows/ci.yml`
- [ ] Passo `restore`
- [ ] Passo `build`
- [ ] Passo testes unitários
- [ ] Passo testes de arquitetura
- [ ] Passo testes de integração
- [ ] Executar o pipeline em um Pull Request de teste
- [ ] Proteger a `master` exigindo CI verde

### Definition of Done da etapa

- [ ] `dotnet build` verde
- [ ] `dotnet test` verde
- [ ] `docker compose up -d` sobe todo o ambiente
- [ ] CI verde em Pull Request
- [ ] Estrutura idêntica à prevista em [`architecture.md`](./architecture.md) §8

---

## Etapa 6 — Domínio (TDD)

### `Money`

- [ ] RED: valor negativo é inválido (RN-001)
- [ ] RED: valor zero é inválido (RN-001)
- [ ] RED: precisão de duas casas decimais (ADR-013)
- [ ] RED: igualdade por valor
- [ ] GREEN + REFACTOR

### `TransactionType`

- [ ] RED: aceita apenas `CREDIT` e `DEBIT` (RN-002)
- [ ] RED: valor inválido é rejeitado
- [ ] RED: o sinal deriva do tipo, nunca do valor (RN-003)
- [ ] GREEN + REFACTOR

### `Transaction`

- [ ] RED: criação válida
- [ ] RED: `Amount` obrigatório e positivo
- [ ] RED: `Type` obrigatório e válido
- [ ] RED: `OccurredAt` obrigatório
- [ ] RED: `OccurredAt` determina o dia da consolidação (RN-004)
- [ ] RED: `Description` opcional
- [ ] RED: imutabilidade após a criação (premissa P-05)
- [ ] GREEN + REFACTOR

### `DailyBalance`

- [ ] RED: saldo = créditos − débitos (RF-004)
- [ ] RED: aplicar crédito
- [ ] RED: aplicar débito
- [ ] RED: saldo pode ser negativo
- [ ] RED: dia sem lançamentos
- [ ] GREEN + REFACTOR

### Exceções de domínio

- [ ] Definir hierarquia de exceções de domínio
- [ ] Testar mensagem e tipo de cada violação de regra

### Definition of Done da etapa

- [ ] RN-001 a RN-004 cobertas por teste
- [ ] Nenhum teste de domínio depende de I/O
- [ ] Testes de arquitetura continuam verdes
- [ ] CI verde

---

## Etapa 7 — Casos de uso (TDD)

### Portas

- [ ] `ITransactionRepository`
- [ ] `IOutboxRepository`
- [ ] `IEventPublisher`
- [ ] `IDailyBalanceRepository`
- [ ] `IProcessedEventRepository`
- [ ] `IUnitOfWork` (ou equivalente para atomicidade)

### `RegisterTransaction` (UC-01)

- [ ] RED: registra lançamento válido
- [ ] RED: grava lançamento e evento na mesma transação (ADR-004)
- [ ] RED: rejeita valor inválido
- [ ] RED: rejeita tipo inválido
- [ ] RED: não depende da disponibilidade do broker (RNF-001)
- [ ] GREEN + REFACTOR

### `ListTransactions` (UC-03)

- [ ] RED: lista com paginação
- [ ] RED: filtra por período
- [ ] RED: retorna coleção vazia sem erro
- [ ] GREEN + REFACTOR

### `ConsolidateTransaction` (UC-04)

- [ ] RED: crédito aumenta o saldo do dia
- [ ] RED: débito reduz o saldo do dia
- [ ] RED: evento repetido não altera o saldo duas vezes (RNF-008)
- [ ] RED: primeiro lançamento do dia cria o saldo
- [ ] RED: usa `data.occurredAt` para determinar o dia (RN-004)
- [ ] GREEN + REFACTOR

### `GetDailyBalance` (UC-02)

- [ ] RED: retorna saldo existente
- [ ] RED: comportamento definido para dia sem lançamentos
- [ ] RED: expõe `updatedAt` (ADR-006)
- [ ] GREEN + REFACTOR

### `PublishPendingOutboxMessages` (UC-05)

- [ ] RED: publica mensagens pendentes
- [ ] RED: marca como processada após confirmação
- [ ] RED: falha de publicação mantém a mensagem pendente (RNF-007)
- [ ] RED: incrementa tentativas e registra erro
- [ ] GREEN + REFACTOR

### Definition of Done da etapa

- [ ] Todos os casos de uso testados com dublês
- [ ] Nenhuma dependência de infraestrutura na camada de aplicação
- [ ] Testes de arquitetura continuam verdes
- [ ] CI verde

---

## Etapa 8 — Infraestrutura de lançamentos

- [ ] `CashFlowDbContext`
- [ ] Mapeamento de `transactions`
- [ ] Mapeamento de `outbox_messages`
- [ ] Migration inicial do `cashflow_db`
- [ ] `TransactionRepository`
- [ ] `OutboxRepository`
- [ ] Unidade de trabalho garantindo atomicidade
- [ ] Configurar Testcontainers (ADR-008)
- [ ] Integração: persistência e leitura de lançamento
- [ ] Integração: precisão de `numeric(18,2)` (ADR-013)
- [ ] Integração: paginação e filtro por período
- [ ] Integração: gravação atômica lançamento + outbox
- [ ] `ConsolidationDbContext`
- [ ] Mapeamento de `daily_balances`
- [ ] Mapeamento de `processed_events`
- [ ] Migration inicial do `consolidation_db`
- [ ] Definir a estratégia de aplicação das migrations
- [ ] CI verde com testes de integração

## Etapa 9 — Mensageria e outbox

- [ ] Definir a topologia RabbitMQ: exchange, fila e DLQ (ADR-003)
- [ ] Declarar a topologia na inicialização
- [ ] Serialização do envelope conforme `docs/api-contracts.md`
- [ ] `RabbitMqEventPublisher` com publisher confirms
- [ ] `OutboxPublisherService` como background service
- [ ] Intervalo de varredura configurável
- [ ] Retry com backoff na publicação
- [ ] Registro de tentativas e do último erro
- [ ] Integração: evento publicado após o registro do lançamento
- [ ] Integração: broker fora do ar → `POST` retorna `201` (RNF-001)
- [ ] Integração: mensagens pendentes são publicadas quando o broker retorna (RNF-007)
- [ ] Log estruturado do ciclo de publicação
- [ ] Registrar `SELECT ... FOR UPDATE SKIP LOCKED` como melhoria posterior, não pré-requisito (ADR-004)

## Etapa 10 — Consolidação e idempotência

- [ ] Consumidor com ack manual
- [ ] Desserialização e validação do envelope
- [ ] Verificação em `processed_events` antes de aplicar
- [ ] Aplicação do evento e gravação do `event_id` na mesma transação (ADR-007)
- [ ] Upsert atômico do saldo diário
- [ ] **Especificar** o mecanismo de retry antes de implementá-lo
- [ ] Avaliar fila de retry com TTL + dead-letter exchange
- [ ] Avaliar contagem de tentativas via header `x-death`
- [ ] Avaliar retry in-process com espera limitada
- [ ] Registrar a escolha e o motivo em [ADR-003](./decisions/ADR-003-messaging.md)
- [ ] Definir limite de tentativas antes da DLQ
- [ ] Integração: mesmo evento N vezes altera o saldo uma única vez (RNF-008)
- [ ] Integração: mensagem inválida chega à DLQ em tempo finito
- [ ] Integração: ausência de laço quente entre falha e reentrega
- [ ] Log estruturado do consumo, com `correlationId`

## Etapa 11 — APIs HTTP

### Cash Flow API

- [ ] `POST /transactions` conforme contrato
- [ ] `GET /transactions` conforme contrato
- [ ] Validação de entrada
- [ ] Middleware de exceção → Problem Details
- [ ] Swagger
- [ ] Integração com `WebApplicationFactory`

### Consolidation API

- [ ] `GET /daily-balances/{date}` conforme contrato
- [ ] Validação do formato de data
- [ ] Middleware de exceção → Problem Details
- [ ] Swagger
- [ ] Integração com `WebApplicationFactory`

### Fluxo ponta a ponta

- [ ] Integração: `POST /transactions` → evento → `GET /daily-balances/{date}`
- [ ] Integração: consolidação responde com o serviço de lançamentos fora do ar (RF-006)
- [ ] CI verde

## Etapa 12 — Resiliência e observabilidade

- [ ] Serilog com saída estruturada em JSON (ADR-011)
- [ ] `correlationId` gerado ou propagado na entrada da API
- [ ] `correlationId` propagado até o outbox
- [ ] `correlationId` propagado no envelope do evento
- [ ] `correlationId` propagado no worker
- [ ] Health check `live` em cada serviço
- [ ] Health check `ready` verificando dependências
- [ ] Executar o cenário: Consolidation API fora do ar
- [ ] Executar o cenário: Consolidation Worker fora do ar
- [ ] Executar o cenário: `consolidation_db` fora do ar
- [ ] Executar o cenário: RabbitMQ fora do ar
- [ ] Documentar os resultados contra a tabela de [`architecture.md`](./architecture.md) §6

## Etapa 13 — Testes de carga

- [ ] Criar o diretório `k6/`
- [ ] Cenário: 50 req/s em `POST /transactions`
- [ ] Cenário: 50 eventos/s de ingestão
- [ ] Cenário: 50 req/s em `GET /daily-balances/{date}`
- [ ] Definir os thresholds no script (ADR-010)
- [ ] Executar e coletar os resultados
- [ ] Verificar perda de eventos igual a zero
- [ ] Registrar os resultados no README
- [ ] Registrar as limitações do ambiente de medição

## Etapa 14 — README final e revisão

- [ ] README com instruções de execução verificadas em clone limpo
- [ ] README com funcionamento, decisões e trade-offs
- [ ] README com resultados de testes e de carga
- [ ] Melhorias futuras revisadas
- [ ] Revisar cada ADR contra o que foi realmente implementado
- [ ] Decisão alterada durante a implementação vira ADR nova, não edição silenciosa
- [ ] Diagramas finais conferidos contra o código
- [ ] Revisar [`scope.md`](./scope.md) contra o entregue
- [ ] Decidir sobre a permanência de `AGENTS.md`, `CLAUDE.md` e `.claude/`
- [ ] Repositório público no GitHub
- [ ] Validação final: clone limpo → `docker compose up -d` → sistema funcional
