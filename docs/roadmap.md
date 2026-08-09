# Roadmap de Execução

> Ordem deliberada de construção do projeto. A documentação vem antes do código
> por decisão: a arquitetura deste desafio é o que está sendo avaliado, e decidir
> enquanto se implementa produz justificativa retroativa em vez de decisão.
>
> Este documento é a **visão estratégica** — as fases e o porquê da ordem.
> O detalhamento item a item de cada etapa fica em
> [`progress.md`](./progress.md), para que esta leitura continue sendo curta.

## Visão geral

```
[✓] Etapa 1  Entendimento do desafio
[✓] Etapa 2  Mapeamento de requisitos
[✓] Etapa 3  Decisões arquiteturais (ADRs)
[✓] Etapa 4  Desenho dos contratos de API e eventos
[✓] Etapa 5  Esqueleto da solução, ambiente e CI
[✓] Etapa 6  Domínio (TDD)
[✓] Etapa 7  Casos de uso (TDD)
[✓] Etapa 8  Infraestrutura de lançamentos
[✓] Etapa 9  Mensageria e outbox
[✓] Etapa 10 Consolidação e idempotência
[ ] Etapa 11 APIs HTTP
[ ] Etapa 12 Resiliência e observabilidade
[ ] Etapa 13 Testes de carga
[ ] Etapa 14 README final e revisão
```

Legenda: `[✓]` concluída · `[~]` em andamento · `[ ]` pendente. O detalhamento
item a item está em [`progress.md`](./progress.md).

---

## Etapa 1 — Entendimento do desafio ✓

Leitura do enunciado, identificação dos requisitos explícitos e das ambiguidades.

**Entregue:** [`challenge/`](./challenge/), premissas registradas em [`requirements.md`](./requirements.md).

## Etapa 2 — Mapeamento de requisitos ✓

Tradução do enunciado em RF, RNF e restrições técnicas, com rastreabilidade e
escopo fechado.

**Entregue:** [`requirements.md`](./requirements.md), [`scope.md`](./scope.md).

## Etapa 3 — Decisões arquiteturais ✓

Registro das decisões com alternativas, consequências e trade-offs.

**Entregue:** [`architecture.md`](./architecture.md), [`decisions/`](./decisions/README.md) (ADR-001 a ADR-013), [`testing-strategy.md`](./testing-strategy.md).

## Etapa 4 — Contratos de API e eventos ✓

Definir antes de implementar, porque o contrato é a fronteira entre os dois
contextos e mudá-lo depois custa retrabalho dos dois lados.

- Contrato REST das duas APIs (rota, payload, códigos de status, formato de erro)
- Formato de erro padronizado (RFC 7807 / Problem Details)
- Envelope e versionamento do evento de integração
- Especificação OpenAPI

**Entregue:** [`api-contracts.md`](./api-contracts.md),
[ADR-014](./decisions/ADR-014-cursor-pagination.md).

## Etapa 5 — Esqueleto da solução, ambiente e CI ✓

```
.github/
└── workflows/
    └── ci.yml
src/
tests/
CashFlow.sln
Directory.Build.props
.editorconfig
global.json
docker-compose.yml
```

- Estrutura de projetos conforme [`architecture.md`](./architecture.md) §8
- `global.json` (.NET 10), `Directory.Build.props`, `.editorconfig`
- Projetos de teste vazios e rodando
- `docker-compose.yml` com bancos e broker
- Testes de arquitetura já ativos — as fronteiras passam a ser protegidas desde o
  primeiro commit de código, e não depois que forem violadas
- **CI no GitHub Actions** desde já: `restore → build → unitários → arquitetura →
  integração`, conforme [`testing-strategy.md`](./testing-strategy.md) §5
- `master` protegida: merge apenas com CI verde

A CI entra aqui, e não no fim, porque `dotnet build` e `dotnet test` já foram
declarados como critério de qualidade nas etapas anteriores. Critério de qualidade
que não é executado automaticamente é intenção, não garantia.

**Critério:** `dotnet build` e `docker compose up -d` funcionam, e o pipeline roda
verde em um Pull Request de teste.

## Etapa 6 — Domínio (TDD) ✓

- `Money`, `TransactionType`, `Transaction`
- `DailyBalance`
- Exceções de domínio

**Critério:** todas as regras RN-001 a RN-004 cobertas; nenhum teste precisa de I/O.

## Etapa 7 — Casos de uso (TDD) ✓

- `RegisterTransaction`, `ListTransactions`
- `ConsolidateTransaction`, `GetDailyBalance`
- Portas: `ITransactionRepository`, `IOutboxRepository`, `IEventPublisher`,
  `IDailyBalanceRepository`, `IProcessedEventRepository`

**Critério:** casos de uso testados com dublês; nenhuma dependência de infraestrutura.

## Etapa 8 — Infraestrutura de lançamentos ✓

- `DbContext`, mapeamentos, migrations
- Repositórios sobre PostgreSQL
- Testes de integração com Testcontainers

**Critério:** persistência real validada, incluindo precisão de `numeric(18,2)`.

## Etapa 9 — Mensageria e outbox ✓

- Tabela e repositório de outbox
- Gravação atômica lançamento + evento
- `OutboxPublisherService` com publisher confirms e retry
- Topologia RabbitMQ (exchange, fila, DLQ)

Ordem deliberada: **primeiro o fluxo simples com uma única instância do
publisher**. `SELECT ... FOR UPDATE SKIP LOCKED` e concorrência entre múltiplos
publishers só entram depois de o caminho básico estar funcionando e testado — a
idempotência do consumidor já cobre a duplicação eventual, então a concorrência é
otimização, não pré-requisito ([ADR-004](./decisions/ADR-004-transactional-outbox.md)).

**Critério:** com o broker fora do ar, `POST` retorna `201` e as mensagens são
publicadas quando ele volta. Este é o primeiro ponto em que RNF-001 fica
demonstrável.

## Etapa 10 — Consolidação e idempotência ✓

- Consumidor com ack manual
- `processed_events` e transação única
- Upsert atômico do saldo diário
- Retry in-process com espera limitada, e DLQ ao esgotar as tentativas

`nack` com `requeue=true` não produz backoff: a mensagem volta para a frente da
fila e é reentregue de imediato, criando um laço quente entre falha e reentrega.
O backoff precisa vir de um mecanismo explícito:

```
erro transitório  →  retry controlado (com espera real entre tentativas)
                          ↓
                  limite de tentativas atingido
                          ↓
                         DLQ
```

Entre os três mecanismos que a [ADR-003](./decisions/ADR-003-messaging.md) deixou
em aberto, vale o mais simples: **retry in-process com espera limitada**, seguido
de `nack(requeue=false)` para a DLQ. Ele não exige topologia extra e prova as três
coisas que precisam ser provadas — mensagem não some, mensagem repetida não
duplica saldo, mensagem problemática não trava a fila. Fila de retry com TTL e
leitura de `x-death` resolveriam o mesmo com mais peças. O motivo é registrado na
ADR-003 quando o consumidor for implementado.

**Critério:** mesmo evento publicado N vezes altera o saldo uma única vez, e uma
mensagem que falha sempre chega à DLQ em tempo finito, sem laço quente.

## Etapa 11 — APIs HTTP

- Endpoints, validação de entrada, Problem Details
- Swagger
- Testes de integração com `WebApplicationFactory`

**Critério:** fluxo completo `POST /transactions` → `GET /daily-balances/{date}`.

## Etapa 12 — Resiliência e observabilidade

- `ILogger` com saída JSON estruturada e correlation id ponta a ponta
- Health checks `live` e `ready`
- Cenários de falha executados e documentados

**Critério:** a tabela de comportamento sob falha de [`architecture.md`](./architecture.md) §6
é reproduzida na prática.

## Etapa 13 — Testes de carga

- Um cenário k6 obrigatório: 50 req/s em `GET /daily-balances/{date}`
- Execução, coleta e registro dos resultados

"50 chamadas por segundo" é ambíguo no enunciado. Em vez de cobrir as três
leituras possíveis ([ADR-010](./decisions/ADR-010-performance-validation.md)),
provamos bem a principal — a consolidação sob carga — e registramos a ambiguidade
no README. A convergência ponta a ponta é provada por teste funcional na etapa 11,
que não precisa de carga para ser convincente. Carga de escrita e de ingestão são
extras, executados se sobrar tempo.

**Critério:** 50 req/s com erro < 1%.

## Etapa 14 — README final e revisão

- README com execução, funcionamento, decisões, trade-offs e melhorias futuras
- Revisão das ADRs contra o que foi realmente implementado — decisão que mudou
  durante a implementação vira ADR nova, não edição silenciosa
- Diagramas finais
- Decidir sobre a permanência dos arquivos de instrução de agentes (`AGENTS.md`,
  `CLAUDE.md`, `.claude/`): são ferramenta de processo, não parte da solução
- Repositório público no GitHub

**Critério:** clone limpo → `docker compose up -d` → sistema funcional.

---

## Ordem de prioridade sob restrição de tempo

A arquitetura tem muitas partes móveis. Se o tempo apertar, o corte é feito de
baixo para cima nesta ordem — e nunca de cima para baixo:

```
CORRETUDE      lançamento, consolidação, saldo correto
     >
TESTES         domínio, casos de uso, integração dos fluxos críticos
     >
RESILIÊNCIA    outbox, idempotência, comportamento com broker fora do ar
     >
DOCUMENTAÇÃO   README, ADRs, diagramas
     >
EXTRAS         k6, tracing, dashboards, reprocessamento automático da DLQ
```

O enunciado admite que melhorias possam ser apenas **documentadas** em vez de
implementadas. Entregar o núcleo funcionando e registrar o resto como melhoria
futura é uma entrega melhor do que entregar tudo pela metade.

---

## Riscos identificados

| Risco | Impacto | Mitigação |
|-------|---------|-----------|
| Complexidade arquitetural consumir o tempo do desafio | Entrega incompleta | Escopo funcional mínimo fechado em [`scope.md`](./scope.md) |
| Testcontainers deixar a suíte lenta | Perda de ritmo no TDD | Separar categorias; unitários rodam sem Docker |
| Consistência eventual confundir na avaliação | Parecer defeito | Documentada e exposta via `updatedAt` na resposta |
| Over-engineering percebido | Avaliação negativa | Cada peça amarrada a um requisito na matriz de rastreabilidade |
| Ambiente com poucos recursos para 6 containers | Falha ao subir | Imagens alpine; limites documentados no README |
