# Progresso de Implementação

> Backlog executável do projeto. Enquanto [`roadmap.md`](./roadmap.md) responde
> *"quais são as grandes fases e por que nesta ordem"*, este documento responde
> *"qual é exatamente o próximo item a fazer"*.
>
> Regra de uso: **este arquivo é atualizado no mesmo Pull Request que entrega o
> item**. Checkbox marcado sem entrega correspondente é ruído, não progresso.

**Etapa atual: 13 — Testes de carga**

## Progresso macro

```
[x] Etapa 1  Entendimento do desafio
[x] Etapa 2  Mapeamento de requisitos
[x] Etapa 3  Decisões arquiteturais (ADRs)
[x] Etapa 4  Contratos de API e eventos
[x] Etapa 5  Esqueleto da solução, ambiente e CI
[x] Etapa 6  Domínio (TDD)
[x] Etapa 7  Casos de uso (TDD)
[x] Etapa 8  Infraestrutura de lançamentos
[x] Etapa 9  Mensageria e outbox
[x] Etapa 10 Consolidação e idempotência
[x] Etapa 11 APIs HTTP
[x] Etapa 12 Resiliência e observabilidade
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
- [x] Definir política para lançamento retroativo (premissa P-06) — sem teto de
      retroatividade
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
> Não há janela de retroatividade: qualquer `occurredAt` válido é aceito. O teto
> de 365 dias que o contrato previa era regra inventada por nós e foi removido
> antes de virar código.

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

## Etapa 7 — Casos de uso (TDD) ✅

> Casos de uso sinalizam falha por **exceção** e ausência por **retorno nulável**.
> Violação de regra sobe como `DomainException` e vira `400` no middleware da
> etapa 11; parâmetro de consulta fora do contrato sobe como `InvalidQueryException`.
> A alternativa — `Result<T>` em todo handler — obrigaria cada caso de uso a
> capturar o que o domínio acabou de sinalizar, só para reembalar. A linha
> "Result pattern" da matriz de RNF-011 em [`requirements.md`](./requirements.md)
> foi corrigida para descrever o que existe.

### Portas

- [x] `ITransactionRepository`
- [x] `IOutboxRepository`
- [x] `IEventPublisher`
- [x] `IDailyBalanceRepository`
- [x] `IProcessedEventRepository`
- [x] `IUnitOfWork` (ou equivalente para atomicidade)

> `IUnitOfWork` é por contexto, como o resto: o da consolidação nasce com o seu
> caso de uso. Cada porta apareceu quando um teste precisou dela — `ListAsync` e
> `GetByIdAsync` só entraram em `ITransactionRepository` nos casos de uso que as
> exigiram, e não junto com `AddAsync`.

### `RegisterTransaction` (UC-01)

- [x] RED: registra lançamento válido
- [x] RED: grava lançamento e evento na mesma transação (ADR-004)
- [x] RED: rejeita valor inválido
- [x] RED: rejeita tipo inválido
- [x] RED: não depende da disponibilidade do broker (RNF-001)
- [x] GREEN + REFACTOR

> RNF-001 é verificada pela **ausência da dependência**: um teste falha se
> `IEventPublisher` aparecer no construtor do caso de uso. Teste de comportamento
> provaria que o broker não é chamado hoje; este prova que não há por onde chamar.
>
> O payload gravado no outbox é conferido campo a campo contra
> [`api-contracts.md`](./api-contracts.md) §5, incluindo a distinção entre o
> `occurredAt` do envelope (emissão) e o de `data` (fato econômico) — o ponto
> mais fácil de errar do contrato, e o único que produziria saldo errado sem
> falhar em lugar nenhum.
>
> O instante do servidor quando `occurredAt` é omitido (premissa P-08) ficou no
> caso de uso, e não na borda HTTP: é política de aplicação, não de transporte.

### `ListTransactions` (UC-03)

- [x] RED: lista a primeira página sem cursor
- [x] RED: continua a partir do cursor sem repetir nem pular registros (ADR-014)
- [x] RED: desempata por `id` quando `occurredAt` é idêntico
- [x] RED: última página devolve `nextCursor` nulo e `hasMore` falso
- [x] RED: cursor inválido é rejeitado
- [x] RED: filtra por período
- [x] RED: retorna coleção vazia sem erro
- [x] GREEN + REFACTOR

> A consulta pede `limit + 1` registros ao repositório. É o que distingue "a
> página encheu" de "acabou" sem custar um `COUNT(*)` — o custo O(n) que a
> paginação por cursor existe para evitar.
>
> `limit` fora de `[1, 200]`, período invertido e cursor ilegível são validados
> aqui, e não só na borda HTTP: o `400` da etapa 11 vira eco de uma regra única,
> como já acontece com `Money`.

### `GetTransaction` (UC-06)

- [x] RED: retorna o lançamento existente
- [x] RED: lançamento inexistente não é erro de aplicação, é ausência
- [x] GREEN + REFACTOR

### `ConsolidateTransaction` (UC-04)

- [x] RED: crédito aumenta o saldo do dia
- [x] RED: débito reduz o saldo do dia
- [x] RED: evento repetido não altera o saldo duas vezes (RNF-008)
- [x] RED: primeiro lançamento do dia cria o saldo
- [x] RED: usa `data.occurredAt` para determinar o dia (RN-004)
- [x] GREEN + REFACTOR

> O caso de uso recebe o evento como ele chega do contrato. Traduzi-lo para um
> comando idêntico campo a campo seria indireção sem ganho — `Shared.Contracts`
> existe justamente para ser a fronteira compartilhada.
>
> A consulta a `processed_events` antes de aplicar é o **caminho barato**, não a
> garantia: duas mensagens concorrentes podem passar as duas pela verificação, e
> quem decide é a chave primária no commit
> ([ADR-007](./decisions/ADR-007-idempotency.md)). O tratamento da violação de
> unicidade é da etapa 10, com banco real — aqui ele não teria o que exercitar.
>
> Há teste de que dois eventos **distintos** sobre o mesmo lançamento são ambos
> aplicados: `eventId` identifica a mensagem, não o lançamento (contrato §5.3).
> Usar `transactionId` como chave de idempotência funcionaria hoje e quebraria no
> primeiro evento adicional.

### `GetDailyBalance` (UC-02)

- [x] RED: retorna saldo existente
- [x] RED: comportamento definido para dia sem lançamentos
- [x] RED: expõe `updatedAt` (ADR-006)
- [x] GREEN + REFACTOR

> A consulta sempre devolve um saldo, inclusive para data futura: o contrato não
> distingue "ainda não aconteceu" de "não houve movimentação", e a distinção não
> mudaria o número.

### `PublishPendingOutboxMessages` (UC-05)

- [x] RED: publica mensagens pendentes
- [x] RED: marca como processada após confirmação
- [x] RED: falha de publicação mantém a mensagem pendente (RNF-007)
- [x] RED: incrementa tentativas e registra erro
- [x] GREEN + REFACTOR

> Uma mensagem que falha não interrompe o lote, e o `SaveChanges` acontece uma
> vez ao fim: publicação confirmada seguida de falha ao gravar faz a mensagem ser
> republicada depois, o que a entrega *at-least-once* já prevê e a idempotência
> do consumidor absorve ([ADR-007](./decisions/ADR-007-idempotency.md)).

### Definition of Done da etapa

- [x] Todos os casos de uso testados com dublês
- [x] Nenhuma dependência de infraestrutura na camada de aplicação
- [x] Testes de arquitetura continuam verdes
- [x] CI verde

---

## Etapa 8 — Infraestrutura de lançamentos ✅

### Cash Flow

- [x] `CashFlowDbContext`
- [x] Mapeamento de `transactions`
- [x] Mapeamento de `outbox_messages`
- [x] Migration inicial do `cashflow_db`
- [x] `TransactionRepository`
- [x] `OutboxRepository`
- [x] Unidade de trabalho garantindo atomicidade
- [x] Configurar Testcontainers (ADR-008)
- [x] Integração: persistência e leitura de lançamento
- [x] Integração: precisão de `numeric(18,2)` (ADR-013)
- [x] Índice `(occurred_at DESC, id DESC)` para a paginação por cursor (ADR-014)
- [x] Integração: paginação por cursor e filtro por período
- [x] Integração: inserção concorrente não duplica registro entre páginas (ADR-014)
- [x] Integração: gravação atômica lançamento + outbox

### Consolidation

- [x] `ConsolidationDbContext`
- [x] Mapeamento de `daily_balances`
- [x] Mapeamento de `processed_events`
- [x] Migration inicial do `consolidation_db`
- [x] `DailyBalanceRepository`
- [x] `ProcessedEventRepository`
- [x] Unidade de trabalho garantindo atomicidade
- [x] Integração: persistência e leitura do saldo diário
- [x] Integração: chave primária de `processed_events` recusa evento repetido (ADR-007)
- [x] Definir a estratégia de aplicação das migrations
- [x] CI verde com testes de integração

## Etapa 9 — Mensageria e outbox ✅

- [x] Definir a topologia RabbitMQ: exchange, fila e DLQ (ADR-003)
- [x] Declarar a topologia na conexão
- [x] Serialização do envelope conforme `docs/api-contracts.md`
- [x] `RabbitMqEventPublisher` com publisher confirms
- [x] `OutboxPublisherService` como background service
- [x] Intervalo de varredura configurável
- [x] Retry com backoff na publicação
- [x] Registro de tentativas e do último erro
- [x] Integração: evento publicado após o registro do lançamento
- [x] Integração: broker fora do ar → lançamento é registrado e o evento fica pendente (RNF-001)
- [x] Integração: mensagens pendentes são publicadas quando o broker retorna (RNF-007)
- [x] Log estruturado do ciclo de publicação
- [x] Registrar `SELECT ... FOR UPDATE SKIP LOCKED` como melhoria posterior, não pré-requisito (ADR-004)

> A topologia é declarada **a cada conexão**, não uma vez na inicialização. É
> idempotente no RabbitMQ, e um broker que voltou sem volume persistido precisa
> dela de novo — declarar de menos custa evento perdido, declarar de novo não
> custa nada. Pelo mesmo motivo a conexão é aberta na primeira publicação, e não
> no startup: tornar o broker pré-condição para subir contradiria RNF-001.
>
> `POST /transactions` não existe até a etapa 11, então RNF-001 é verificada onde
> ela já é verificável: o lançamento é gravado com o broker fora do ar e o evento
> fica pendente, com tentativa e erro registrados.

## Etapa 10 — Consolidação e idempotência ✅

- [x] Consumidor com ack manual
- [x] Desserialização e validação do envelope
- [x] Verificação em `processed_events` antes de aplicar
- [x] Aplicação do evento e gravação do `event_id` na mesma transação (ADR-007)
- [x] Upsert atômico do saldo diário
- [x] Retry in-process com espera limitada, e `nack(requeue=false)` para a DLQ ao
      esgotar as tentativas
- [x] Definir limite de tentativas antes da DLQ
- [x] Registrar a escolha e o motivo em [ADR-003](./decisions/ADR-003-messaging.md)
- [x] Integração: mesmo evento N vezes altera o saldo uma única vez (RNF-008)
- [x] Integração: mensagem inválida chega à DLQ em tempo finito
- [x] Integração: ausência de laço quente entre falha e reentrega
- [x] Log estruturado do consumo, com `correlationId`

> Não há `INSERT ... ON CONFLICT`. O upsert do saldo diário é feito pelo par
> chave primária de `daily_balances` + retry: duas transações que tentem criar a
> linha do mesmo dia disputam, a perdedora é desfeita **inteira** — inclusive a
> marcação do evento — e a tentativa seguinte encontra a linha e soma sobre ela.
> Há teste com 20 eventos concorrentes no mesmo dia, e nenhum se perde.
>
> Erro permanente não passa por retry: envelope ilegível, campo obrigatório
> ausente e violação de regra de domínio vão direto para a DLQ. Um JSON quebrado
> não fica válido na segunda leitura.
>
> A topologia mudou de lugar: era constante em `CashFlow.Infrastructure` e passou
> para `Shared.Contracts`. Ela é contrato — produtor e consumidor precisam
> concordar no nome do exchange e da fila —, e duplicá-la nos dois contextos
> tornaria possível uma divergência que quebraria a integração em silêncio.

## Etapa 11 — APIs HTTP ✅

### Cash Flow API

- [x] `POST /transactions` conforme contrato
- [x] `GET /transactions` conforme contrato
- [x] `GET /transactions/{id}` conforme contrato
- [x] Validação de entrada
- [x] Middleware de exceção → Problem Details
- [x] Middleware de `correlationId` (ADR-011)
- [x] OpenAPI + Swagger UI
- [x] Conferir a OpenAPI gerada contra [`api-contracts.md`](./api-contracts.md)
- [x] Integração com `WebApplicationFactory`

### Consolidation API

- [x] `GET /daily-balances/{date}` conforme contrato
- [x] Validação do formato de data
- [x] Middleware de exceção → Problem Details
- [x] Middleware de `correlationId` (ADR-011)
- [x] OpenAPI + Swagger UI
- [x] Conferir a OpenAPI gerada contra [`api-contracts.md`](./api-contracts.md)
- [x] Integração com `WebApplicationFactory`

### Fluxo ponta a ponta

- [x] Integração: `POST /transactions` → evento → `GET /daily-balances/{date}`
- [x] Integração: consolidação responde com o serviço de lançamentos fora do ar (RF-006)
- [x] CI verde

> Duas correções de contrato apareceram ao implementar:
>
> A rota de consulta por id não usa a restrição `:guid`. Com ela, um id malformado
> não casaria a rota e viraria `404`, mas o contrato distingue os dois casos —
> `400` para formato inválido, `404` para id válido e inexistente (§2.2).
>
> `occurredAt` omitido passa a ser **exatamente** igual a `createdAt`, como o
> contrato promete. Eram duas leituras de relógio e diferiam por microssegundos.
> A política de premissa P-08 desceu do caso de uso para o domínio, que é onde os
> dois campos nascem: manter uma leitura de relógio de cada lado não teria como
> produzir igualdade.
>
> Validar em cascata teria quebrado §4.2. O ambiente real mostrou que `type` e
> `amount` inválidos juntos reportavam só um deles: o domínio para no primeiro
> erro. A borda passa a perguntar campo a campo, usando as mesmas fábricas do
> domínio — todos os campos inválidos vêm de uma vez, e a regra continua com um
> dono só.
>
> RF-006 não é verificada por um teardown. A fixture da Consolidation API não tem
> `cashflow_db`, não tem broker e não carrega assembly algum do outro contexto —
> a independência é a própria montagem do teste.
>
> `EndToEndTests` é o único projeto que referencia os dois contextos, e existe
> porque a integração entre eles não cabe em nenhum dos dois lados. Os projetos de
> produção continuam sem se referenciar.

## Etapa 12 — Resiliência e observabilidade ✅

- [x] `ILogger` com saída estruturada em JSON (`AddJsonConsole`)
- [x] `correlationId` gerado ou propagado na entrada da API
- [x] `correlationId` propagado até o outbox
- [x] `correlationId` propagado no envelope do evento
- [x] `correlationId` propagado no worker
- [x] Health check `live` em cada API
- [x] Health check `ready` verificando dependências
- [x] Executar o cenário: Consolidation API fora do ar
- [x] Executar o cenário: Consolidation Worker fora do ar
- [x] Executar o cenário: `consolidation_db` fora do ar
- [x] Executar o cenário: RabbitMQ fora do ar
- [x] Documentar os resultados contra a tabela de [`architecture.md`](./architecture.md) §6

> Os cenários encontraram um defeito real. Com o `consolidation_db` fora do ar por
> 40 segundos, o evento ia para a DLQ e o saldo não convergia: a janela de retry
> do consumidor era menor que a queda. A DLQ é para mensagem problemática, não
> para infraestrutura indisponível — um evento válido não pode virar trabalho
> manual porque o banco piscou. O consumidor passou a distinguir os dois casos por
> `DbException.IsTransient` e devolve a mensagem à fila quando a falha é de
> conectividade.
>
> O worker não tem endpoint de health. Ele não expõe HTTP, e acrescentar um
> servidor só para responder "estou vivo" repetiria o que o Docker já sabe pelo
> estado do container. O que um `ready` de worker acrescentaria — "vivo mas
> travado" — já é observável pelos sinais que a ADR-011 lista: profundidade da
> fila e defasagem de `updatedAt`.
>
> A imagem de runtime não trazia cliente HTTP, então o `healthcheck` do Compose
> não tinha como perguntar nada ao processo. Os Dockerfiles das APIs passaram a
> instalar `curl`.

## Etapa 13 — Testes de carga

- [ ] Criar o diretório `k6/`
- [ ] Cenário obrigatório: 50 req/s em `GET /daily-balances/{date}`
- [ ] Definir os thresholds no script (ADR-010)
- [ ] Executar e coletar os resultados
- [ ] Registrar os resultados e as limitações do ambiente no README
- [ ] Registrar no README a interpretação adotada da ambiguidade do enunciado

Extras, apenas se sobrar tempo:

- [ ] Cenário: 50 req/s em `POST /transactions`
- [ ] Cenário: 50 eventos/s de ingestão, com perda igual a zero

> "50 chamadas por segundo" é ambíguo no enunciado. Em vez de cobrir as três
> leituras possíveis, provamos bem a principal — a consolidação sob carga — e
> registramos a ambiguidade. A convergência ponta a ponta é provada por teste
> funcional na etapa 11, que não precisa de carga para ser convincente.

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
