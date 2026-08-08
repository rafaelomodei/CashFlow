# Roadmap de Execução

> Ordem deliberada de construção do projeto. A documentação vem antes do código
> por decisão: a arquitetura deste desafio é o que está sendo avaliado, e decidir
> enquanto se implementa produz justificativa retroativa em vez de decisão.

## Visão geral

```
[✓] Etapa 1  Entendimento do desafio
[✓] Etapa 2  Mapeamento de requisitos
[✓] Etapa 3  Decisões arquiteturais (ADRs)
[ ] Etapa 4  Desenho dos contratos de API e eventos
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

## Etapa 4 — Contratos de API e eventos

Definir antes de implementar, porque o contrato é a fronteira entre os dois
contextos e mudá-lo depois custa retrabalho dos dois lados.

- Contrato REST das duas APIs (rota, payload, códigos de status, formato de erro)
- Formato de erro padronizado (RFC 7807 / Problem Details)
- Envelope e versionamento do evento de integração
- Especificação OpenAPI

**Saída:** `docs/api-contracts.md`

## Etapa 5 — Esqueleto da solução, ambiente e CI

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

## Etapa 6 — Domínio (TDD)

- `Money`, `TransactionType`, `Transaction`
- `DailyBalance`
- Exceções de domínio

**Critério:** todas as regras RN-001 a RN-004 cobertas; nenhum teste precisa de I/O.

## Etapa 7 — Casos de uso (TDD)

- `RegisterTransaction`, `ListTransactions`
- `ConsolidateTransaction`, `GetDailyBalance`
- Portas: `ITransactionRepository`, `IOutboxRepository`, `IEventPublisher`,
  `IDailyBalanceRepository`, `IProcessedEventRepository`

**Critério:** casos de uso testados com dublês; nenhuma dependência de infraestrutura.

## Etapa 8 — Infraestrutura de lançamentos

- `DbContext`, mapeamentos, migrations
- Repositórios sobre PostgreSQL
- Testes de integração com Testcontainers

**Critério:** persistência real validada, incluindo precisão de `numeric(18,2)`.

## Etapa 9 — Mensageria e outbox

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

## Etapa 10 — Consolidação e idempotência

- Consumidor com ack manual
- `processed_events` e transação única
- Upsert atômico do saldo diário
- **Especificar o mecanismo concreto de retry** antes de implementá-lo

O item de retry é o que exige mais cuidado nesta etapa. `nack` com `requeue=true`
não produz backoff: a mensagem volta para a frente da fila e é reentregue de
imediato, criando um laço quente entre falha e reentrega. O desenho precisa
definir explicitamente:

```
erro transitório  →  retry controlado (com espera real entre tentativas)
                          ↓
                  limite de tentativas atingido
                          ↓
                         DLQ
```

Alternativas a avaliar na etapa: fila de retry com TTL + dead-letter exchange
(delay queues), contagem de tentativas via header (`x-death`), ou retry
in-process com espera limitada antes do `nack`. A escolha e o motivo entram em
[ADR-003](./decisions/ADR-003-messaging.md).

**Critério:** mesmo evento publicado N vezes altera o saldo uma única vez, e uma
mensagem que falha sempre chega à DLQ em tempo finito, sem laço quente.

## Etapa 11 — APIs HTTP

- Endpoints, validação de entrada, Problem Details
- Swagger
- Testes de integração com `WebApplicationFactory`

**Critério:** fluxo completo `POST /transactions` → `GET /daily-balances/{date}`.

## Etapa 12 — Resiliência e observabilidade

- Serilog estruturado e correlation id ponta a ponta
- Health checks `live` e `ready`
- Cenários de falha executados e documentados

**Critério:** a tabela de comportamento sob falha de [`architecture.md`](./architecture.md) §6
é reproduzida na prática.

## Etapa 13 — Testes de carga

- Cenários k6 conforme [ADR-010](./decisions/ADR-010-performance-validation.md)
- Execução, coleta e registro dos resultados

**Critério:** 50 req/s com erro < 1% e perda de eventos igual a zero.

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
