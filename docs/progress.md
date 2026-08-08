# Progresso de Implementação

> Backlog executável do projeto. Enquanto [`roadmap.md`](./roadmap.md) responde
> *"quais são as grandes fases e por que nesta ordem"*, este documento responde
> *"qual é exatamente o próximo item a fazer"*.
>
> Regra de uso: **este arquivo é atualizado no mesmo Pull Request que entrega o
> item**. Checkbox marcado sem entrega correspondente é ruído, não progresso.

**Etapa atual: 7 — Casos de uso (TDD)**

## Progresso macro

```
[x] Etapa 1  Entendimento do desafio
[x] Etapa 2  Mapeamento de requisitos
[x] Etapa 3  Decisões arquiteturais (ADRs)
[x] Etapa 4  Contratos de API e eventos
[x] Etapa 5  Esqueleto da solução, ambiente e CI
[x] Etapa 6  Domínio (TDD)
[~] Etapa 7  Casos de uso (TDD)
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

## Etapa 4 — Contratos de API e eventos ✅

> Saída: [`api-contracts.md`](./api-contracts.md). Nenhum código nesta etapa — o
> contrato é a fronteira entre os dois contextos e mudá-lo depois custa retrabalho
> dos dois lados.

### Cash Flow API — `POST /transactions`

- [x] Definir rota e verbo
- [x] Definir request DTO
- [x] Definir campos obrigatórios e opcionais
- [x] Definir representação de `CREDIT` / `DEBIT` (RF-002, RN-002)
- [x] Definir representação monetária no contrato (ADR-013, RN-001)
- [x] Definir formato de `occurredAt` (ISO 8601 / UTC, premissa P-04)
- [x] Definir política para lançamento retroativo (premissa P-06) — janela
      configurável, premissa P-09
- [x] Definir comportamento com `occurredAt` ausente (premissa P-08)
- [x] Definir resposta `201 Created`
- [x] Definir header `Location`
- [x] Definir response DTO do recurso criado
- [x] Definir erro para `amount` inválido (RN-001)
- [x] Definir erro para `type` inválido (RN-002)
- [x] Definir erro para `occurredAt` inválido
- [x] Definir limite de tamanho de `description` (premissa P-10)
- [x] Criar exemplo de request
- [x] Criar exemplo de response

### Cash Flow API — `GET /transactions/{id}`

> Endpoint incorporado nesta etapa: `201 Created` com header `Location` exige um
> recurso de destino. Registrado como UC-06 em [`scope.md`](./scope.md).

- [x] Definir rota
- [x] Definir response DTO (idêntico ao do `POST`)
- [x] Definir `404` para lançamento inexistente
- [x] Definir `400` para id fora do formato UUID

### Cash Flow API — `GET /transactions`

- [x] Definir rota
- [x] Definir estratégia de paginação — cursor/keyset ([ADR-014](./decisions/ADR-014-cursor-pagination.md))
- [x] Definir parâmetros de paginação e seus limites
- [x] Definir formato e opacidade do cursor
- [x] Definir filtro `startDate`
- [x] Definir filtro `endDate`
- [x] Definir ordenação padrão
- [x] Definir response DTO da coleção
- [x] Definir metadados de paginação na resposta
- [x] Definir comportamento sem registros
- [x] Definir erro para intervalo de datas inválido
- [x] Definir erro para cursor inválido
- [x] Criar exemplos de request e response

### Consolidation API — `GET /daily-balances/{date}`

- [x] Definir formato da data no path
- [x] Definir response DTO
- [x] Definir `totalCredits`
- [x] Definir `totalDebits`
- [x] Definir `balance` (RF-004)
- [x] Definir `updatedAt` como evidência de consistência eventual (ADR-006)
- [x] Definir comportamento para dia sem lançamentos
- [x] Definir erro para data em formato inválido
- [x] Criar exemplos de request e response

### Erros HTTP

- [x] Adotar Problem Details (RFC 7807)
- [x] Definir estrutura comum de erro
- [x] Definir `400 Bad Request`
- [x] Definir formato dos erros de validação campo a campo
- [x] Definir `404 Not Found` e quando ele se aplica
- [x] Definir `415 Unsupported Media Type`
- [x] Definir `500 Internal Server Error`
- [x] Definir `correlationId` no corpo do erro (ADR-011)
- [x] Criar exemplo de cada formato de erro

### Evento `TransactionRegistered`

- [x] Confirmar o nome do evento
- [x] Definir versionamento do contrato
- [x] Definir `eventId` (base da idempotência — ADR-007)
- [x] Definir `eventType`
- [x] Definir `occurredAt` do envelope (instante da emissão)
- [x] Definir `correlationId` no envelope (ADR-011)
- [x] Definir `data.transactionId`
- [x] Definir `data.occurredAt` (dia da consolidação — RN-004)
- [x] Definir `data.amount`
- [x] Definir `data.type`
- [x] Definir exemplo JSON completo
- [x] Definir as propriedades AMQP da mensagem
- [x] Definir política de compatibilidade e evolução do schema
- [x] Definir routing key / nome do exchange e da fila (ADR-003)

### Especificação e documentação

- [x] Criar [`docs/api-contracts.md`](./api-contracts.md)
- [x] Registrar [ADR-014](./decisions/ADR-014-cursor-pagination.md) — paginação por cursor
- [x] Definir como a especificação OpenAPI será gerada e publicada
- [x] Revisar contratos contra [`requirements.md`](./requirements.md)
- [x] Revisar contratos contra as ADRs
- [x] Revisar contratos contra [`scope.md`](./scope.md)
- [x] Atualizar a seção "API" do README
- [x] Atualizar o envelope do evento em [`architecture.md`](./architecture.md) §10
- [x] Atualizar o índice em [`docs/README.md`](./README.md)
- [x] Atualizar este arquivo

### Definition of Done da etapa

- [x] Todos os endpoints do MVP possuem contrato completo
- [x] Todos os status HTTP possíveis estão definidos
- [x] O evento possui schema estável e versionado
- [x] Ambiguidades de contrato foram eliminadas
- [x] Nenhuma regra de negócio nova foi inventada nesta etapa

---

## Etapa 5 — Esqueleto da solução, ambiente e CI ✅

### Solution e convenções

- [x] Criar `CashFlow.sln`
- [x] Criar `global.json` fixando .NET 10
- [x] Criar `Directory.Build.props`
- [x] Configurar `TreatWarningsAsErrors`
- [x] Configurar `Nullable` e `ImplicitUsings`
- [x] Criar `.editorconfig`
- [x] Atualizar `.gitignore` para artefatos .NET

### Projetos — Cash Flow

- [x] `src/CashFlow.Domain`
- [x] `src/CashFlow.Application`
- [x] `src/CashFlow.Infrastructure`
- [x] `src/CashFlow.Api`

### Projetos — Consolidation

- [x] `src/Consolidation.Domain`
- [x] `src/Consolidation.Application`
- [x] `src/Consolidation.Infrastructure`
- [x] `src/Consolidation.Api`
- [x] `src/Consolidation.Worker`

### Projetos — Shared

- [x] `src/Shared.Contracts` (somente contratos de evento — sem regra de negócio)

### Projetos de teste

- [x] `tests/CashFlow.Domain.UnitTests`
- [x] `tests/CashFlow.Application.UnitTests`
- [x] `tests/Consolidation.Domain.UnitTests`
- [x] `tests/Consolidation.Application.UnitTests`
- [x] `tests/CashFlow.IntegrationTests`
- [x] `tests/Consolidation.IntegrationTests`
- [x] `tests/ArchitectureTests`
- [x] Configurar xUnit, FluentAssertions e NSubstitute (ADR-008)
- [x] Configurar categorias/traits separando unitários de integração

> Testcontainers entra na etapa 8, junto do primeiro teste que precisa de banco
> real — instalá-lo aqui seria dependência sem teste que a exija.

### Fronteiras arquiteturais

- [x] Configurar as referências permitidas entre projetos
- [x] Escrever o primeiro Architecture Test
- [x] Teste: `CashFlow.Domain` não referencia `Infrastructure`
- [x] Teste: `CashFlow.Application` não referencia `Infrastructure`
- [x] Teste: `Consolidation.Domain` não referencia `Infrastructure`
- [x] Teste: `Consolidation.Application` não referencia `Infrastructure`
- [x] Teste: nenhum contexto referencia o outro além de `Shared.Contracts`

> As regras são verificadas sobre os `.csproj`, e não sobre o manifesto do
> assembly compilado: o compilador omite do manifesto as referências que nenhum
> tipo usa, e uma referência proibida em projeto ainda vazio passaria
> despercebida justamente enquanto é mais barata de corrigir. Cada regra foi
> validada introduzindo a violação correspondente e observando o teste reprovar.

### Ambiente Docker

- [x] `docker-compose.yml`
- [x] PostgreSQL `cashflow_db`
- [x] PostgreSQL `consolidation_db`
- [x] RabbitMQ com management plugin
- [x] Health checks dos serviços de infraestrutura
- [x] Volumes nomeados para persistência
- [x] `.env.example`
- [x] `Dockerfile` da Cash Flow API
- [x] `Dockerfile` da Consolidation API
- [x] `Dockerfile` do Consolidation Worker
- [x] Validar consumo de recursos do ambiente completo (risco registrado no roadmap)

> Medição do ambiente completo ocioso: ~240 MiB somando os seis containers
> (RabbitMQ 92 MiB, os dois PostgreSQL 24 MiB cada, os três serviços .NET entre
> 24 e 41 MiB). O risco de recursos do roadmap não se confirmou.
>
> Verificado também o ponto que a [ADR-009](./decisions/ADR-009-containers.md)
> trata como decisivo: com o `rabbitmq` parado, a `cashflow-api` sobe e responde
> normalmente. Nesta etapa isso valida a **configuração do ambiente**; o
> comportamento da aplicação sem broker só fica demonstrável na etapa 9, quando
> existir código de publicação.

### Integração contínua

- [x] Criar `.github/workflows/ci.yml`
- [x] Passo `restore`
- [x] Passo `build`
- [x] Passo testes unitários
- [x] Passo testes de arquitetura
- [x] Passo testes de integração
- [x] Executar o pipeline em um Pull Request de teste
- [x] Proteger a `master` exigindo CI verde

> O gate rápido roda um passo extra para os testes **sem categoria**: um teste
> que esqueça o `[Trait]` não cairia em nenhum filtro e seria ignorado sem
> ninguém perceber. Lacuna silenciosa é pior que teste vermelho.
>
> A `master` exige Pull Request, ambos os jobs de CI verdes sobre a versão mais
> recente da base (`strict`), conversas resolvidas, e recusa push direto,
> force-push e exclusão da branch. A aprovação obrigatória ficou em zero: em
> repositório de autor único, o GitHub não permite aprovar o próprio Pull
> Request, e exigir revisão tornaria todo merge impossível — a regra deixaria de
> proteger e passaria a travar.

### Definition of Done da etapa

- [x] `dotnet build` verde
- [x] `dotnet test` verde
- [x] `docker compose up -d` sobe todo o ambiente
- [x] CI verde em Pull Request
- [x] Estrutura idêntica à prevista em [`architecture.md`](./architecture.md) §8

---

## Etapa 6 — Domínio (TDD) ✅

> Cada contexto tem o seu próprio `Domain`: `Money` e `TransactionType` existem
> nos dois, sem compartilhamento. `Shared.Contracts` carrega contrato de evento,
> nunca tipo de domínio ([ADR-002](./decisions/ADR-002-service-decomposition.md)).
> Duplicação de dois tipos pequenos é o preço da fronteira — um kernel comum
> acoplaria os serviços justamente onde a decomposição existe para separá-los.

### `Money`

- [x] RED: valor negativo é inválido (RN-001)
- [x] RED: valor zero é inválido (RN-001)
- [x] RED: precisão de duas casas decimais (ADR-013)
- [x] RED: igualdade por valor
- [x] GREEN + REFACTOR

> Divergência resolvida nesta etapa: `api-contracts.md` §1.4 promete `400` para
> mais de duas casas decimais, enquanto a ADR-013 dizia "normalizado". Vale o
> contrato — mais de duas casas é exceção de domínio, e "normalizar" passa a
> significar apenas elevar a escala (`10.5` → `10.50`). Registrado como nota de
> esclarecimento na [ADR-013](./decisions/ADR-013-money-representation.md);
> regra de validação não vira ADR nova, por
> [`decisions/README.md`](./decisions/README.md).
>
> `Money` também recusa valor acima de `9999999999999999.99`: sem isso, o
> estouro da faixa de `numeric(18,2)` só apareceria na gravação, como erro de
> banco em vez de violação de regra.
>
> A soma de `Money` não foi implementada — nada no contexto de lançamentos soma
> dinheiro. Ela nasce onde o somador existe, no `DailyBalance` da consolidação.

### `TransactionType`

- [x] RED: aceita apenas `CREDIT` e `DEBIT` (RN-002)
- [x] RED: valor inválido é rejeitado
- [x] RED: o sinal deriva do tipo, nunca do valor (RN-003)
- [x] GREEN + REFACTOR

> `Parse` é sensível a maiúsculas: aceitar `credit` faria o sistema receber uma
> grafia e devolver outra na resposta e no evento.

### `Transaction`

- [x] RED: criação válida
- [x] RED: `Amount` obrigatório e positivo
- [x] RED: `Type` obrigatório e válido
- [x] RED: `OccurredAt` obrigatório
- [x] RED: `OccurredAt` determina o dia da consolidação (RN-004)
- [x] RED: `Description` opcional
- [x] RED: imutabilidade após a criação (premissa P-05)
- [x] GREEN + REFACTOR

> RN-004 é atendida em duas partes: o lançamento normaliza `OccurredAt` para UTC,
> e `DailyBalance.DayOf` deriva o dia dessa data. Os dois lados têm o mesmo teste
> — 22h em Brasília pertence ao dia seguinte —, de modo que a limitação de fuso
> aceita pela ADR-013 fique exercitada, e não apenas descrita.
>
> A janela de retroatividade (premissa P-09) não está no domínio: seus limites
> são configuráveis, e configuração não pertence a uma entidade. Ela é validação
> de entrada, na etapa 11.

### `DailyBalance`

- [x] RED: saldo = créditos − débitos (RF-004)
- [x] RED: aplicar crédito
- [x] RED: aplicar débito
- [x] RED: saldo pode ser negativo
- [x] RED: dia sem lançamentos
- [x] GREEN + REFACTOR

> `Balance` é derivado dos dois totais em vez de armazenado: um campo a mais
> significaria um campo a mais para divergir dos números que o originaram.
>
> Dia sem lançamentos é `DailyBalance.Empty` — totais zerados e `UpdatedAt` nulo,
> não ausência de registro. É o que permite à consulta responder `200` e nunca
> `404` ([ADR-006](./decisions/ADR-006-consistency.md)), sem o cliente precisar
> traduzir "não encontrado" para "zero".
>
> O `Money` da consolidação aceita zero e negativo, ao contrário do `Money` de
> lançamentos: aqui ele mede totais e saldo, e um dia pode legitimamente fechar
> negativo. A regra de valor positivo (RN-001) é do lançamento, e é cobrada em
> `Apply` — evento com valor não positivo corromperia o total em silêncio.

### Exceções de domínio

- [x] Definir hierarquia de exceções de domínio
- [x] Testar mensagem e tipo de cada violação de regra

> Uma raiz `DomainException` por contexto, com uma exceção por regra violada. A
> borda HTTP (etapa 11) precisa distinguir violação de regra — `400` — de falha
> do servidor — `500` — sem conhecer cada regra individualmente; é a raiz que
> torna isso possível.

### Definition of Done da etapa

- [x] RN-001 a RN-004 cobertas por teste
- [x] Nenhum teste de domínio depende de I/O
- [x] Testes de arquitetura continuam verdes
- [x] CI verde

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

- [ ] RED: lista a primeira página sem cursor
- [ ] RED: continua a partir do cursor sem repetir nem pular registros (ADR-014)
- [ ] RED: desempata por `id` quando `occurredAt` é idêntico
- [ ] RED: última página devolve `nextCursor` nulo e `hasMore` falso
- [ ] RED: cursor inválido é rejeitado
- [ ] RED: filtra por período
- [ ] RED: retorna coleção vazia sem erro
- [ ] GREEN + REFACTOR

### `GetTransaction` (UC-06)

- [ ] RED: retorna o lançamento existente
- [ ] RED: lançamento inexistente não é erro de aplicação, é ausência
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
- [ ] Índice `(occurred_at DESC, id DESC)` para a paginação por cursor (ADR-014)
- [ ] Integração: paginação por cursor e filtro por período
- [ ] Integração: inserção concorrente não duplica registro entre páginas (ADR-014)
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
- [ ] `GET /transactions/{id}` conforme contrato
- [ ] Validação de entrada
- [ ] Middleware de exceção → Problem Details
- [ ] Middleware de `correlationId` (ADR-011)
- [ ] OpenAPI + Swagger UI
- [ ] Conferir a OpenAPI gerada contra [`api-contracts.md`](./api-contracts.md)
- [ ] Integração com `WebApplicationFactory`

### Consolidation API

- [ ] `GET /daily-balances/{date}` conforme contrato
- [ ] Validação do formato de data
- [ ] Middleware de exceção → Problem Details
- [ ] Middleware de `correlationId` (ADR-011)
- [ ] OpenAPI + Swagger UI
- [ ] Conferir a OpenAPI gerada contra [`api-contracts.md`](./api-contracts.md)
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
