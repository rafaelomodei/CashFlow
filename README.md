# Cash Flow Challenge

Sistema de gestão de fluxo de caixa: registro de lançamentos de crédito e débito
e relatório de saldo diário consolidado.

> **Estado atual: fase de documentação e desenho arquitetural.**
> Nenhum código de produção foi escrito ainda — por decisão, não por pendência.
> As seções marcadas com ⏳ serão preenchidas conforme as etapas do
> [roadmap](./docs/roadmap.md) avançam.

---

## Sobre o projeto

Um lojista precisa gerenciar o fluxo de caixa do dia a dia, registrando créditos e
débitos, e demanda um relatório com o saldo consolidado diariamente.

O desafio impõe um requisito não funcional que define toda a arquitetura:

> A aplicação de gestão de lançamentos precisa continuar operante **mesmo em caso
> de falha no sistema de consolidação diária**.

Enunciado completo: [`docs/challenge/`](./docs/challenge/desafio-desenvolvedor-software.pdf)

## Requisitos

| Categoria | Documento |
|-----------|-----------|
| Funcionais, não funcionais, restrições, premissas | [`docs/requirements.md`](./docs/requirements.md) |
| Escopo do MVP e o que ficou de fora | [`docs/scope.md`](./docs/scope.md) |

Resumo dos requisitos funcionais:

| ID | Requisito |
|----|-----------|
| RF-001 | Registrar um lançamento financeiro |
| RF-002 | Classificar o lançamento como crédito ou débito |
| RF-003 | Consultar os lançamentos registrados |
| RF-004 | Calcular o saldo consolidado diário |
| RF-005 | Consultar o saldo consolidado de um determinado dia |
| RF-006 | Consultar a consolidação sem depender do serviço de lançamentos |

## Arquitetura

Dois contextos independentes, comunicando-se **apenas** por eventos assíncronos.
Nenhuma chamada HTTP síncrona entre eles, nenhum banco compartilhado.

Detalhamento completo: [`docs/architecture.md`](./docs/architecture.md)

## Diagrama

```mermaid
graph TD
    subgraph CashFlowCtx["Contexto: Lançamentos"]
        API1["Cash Flow API"]
        DB1[("cashflow_db<br/>transactions + outbox")]
        PUB["Outbox Publisher"]
        API1 -->|"lançamento + evento<br/>(mesma transação)"| DB1
        PUB -->|lê pendentes| DB1
    end

    MQ{{"RabbitMQ<br/>+ DLQ"}}

    subgraph ConsolidationCtx["Contexto: Consolidação"]
        WK["Consolidation Worker"]
        DB2[("consolidation_db<br/>daily_balances")]
        API2["Consolidation API"]
        WK -->|upsert idempotente| DB2
        API2 -->|lê saldo| DB2
    end

    PUB -->|publica| MQ
    MQ -->|consome| WK
```

## Tecnologias

| Camada | Tecnologia | Decisão |
|--------|-----------|---------|
| Linguagem / runtime | C# / .NET 10 (LTS) | [ADR-012](./docs/decisions/ADR-012-tech-stack.md) |
| API | ASP.NET Core (controllers) | [ADR-012](./docs/decisions/ADR-012-tech-stack.md) |
| Persistência | PostgreSQL + EF Core | [ADR-005](./docs/decisions/ADR-005-database.md) |
| Mensageria | RabbitMQ | [ADR-003](./docs/decisions/ADR-003-messaging.md) |
| Testes | xUnit, FluentAssertions, NSubstitute, Testcontainers | [ADR-008](./docs/decisions/ADR-008-tdd.md) |
| Carga | k6 | [ADR-010](./docs/decisions/ADR-010-performance-validation.md) |
| Logs | Serilog | [ADR-011](./docs/decisions/ADR-011-observability.md) |
| Ambiente | Docker + Docker Compose | [ADR-009](./docs/decisions/ADR-009-containers.md) |
| CI | GitHub Actions | [`testing-strategy.md`](./docs/testing-strategy.md#integração-contínua) |

## Estrutura do projeto

```
docs/                  documentação, ADRs e enunciado
.github/workflows/     pipeline de CI (⏳ etapa 5)
src/                   código-fonte (⏳ etapa 5)
tests/                 testes automatizados (⏳ etapa 5)
k6/                    testes de carga (⏳ etapa 13)
docker-compose.yml     ambiente local (⏳ etapa 5)
```

Estrutura de projetos planejada: [`docs/architecture.md`](./docs/architecture.md) §8.

## Como executar

⏳ *Etapa 5 do [roadmap](./docs/roadmap.md).*

O objetivo definido em [ADR-009](./docs/decisions/ADR-009-containers.md) é que a
execução completa seja:

```bash
docker compose up -d
```

## API

⏳ *Etapa 4 do [roadmap](./docs/roadmap.md) — contratos ainda em definição.*

Endpoints previstos:

| Método | Rota | Serviço |
|--------|------|---------|
| `POST` | `/transactions` | Cash Flow |
| `GET` | `/transactions` | Cash Flow |
| `GET` | `/daily-balances/{date}` | Consolidation |

## Decisões arquiteturais

13 ADRs documentam contexto, alternativas avaliadas, consequências e trade-offs de
cada escolha: [`docs/decisions/`](./docs/decisions/README.md).

As principais:

- [ADR-001](./docs/decisions/ADR-001-architecture.md) — Clean Architecture com SOLID
- [ADR-002](./docs/decisions/ADR-002-service-decomposition.md) — Dois serviços independentes
- [ADR-004](./docs/decisions/ADR-004-transactional-outbox.md) — Transactional Outbox
- [ADR-007](./docs/decisions/ADR-007-idempotency.md) — Idempotência no consumidor

## Consistência dos dados

O sistema opera com **consistência eventual** entre lançamentos e saldo: um
lançamento recém-registrado leva alguns segundos para refletir no saldo consolidado.
Isso é consequência direta da independência exigida pelo enunciado, e não um
defeito. Detalhes e garantias: [ADR-006](./docs/decisions/ADR-006-consistency.md).

## Resiliência

| Componente fora do ar | `POST /transactions` | `GET /daily-balances` |
|-----------------------|----------------------|------------------------|
| Consolidation API | ✅ funciona | ❌ indisponível |
| Consolidation Worker | ✅ funciona | ⚠️ dados defasados |
| `consolidation_db` | ✅ funciona | ❌ indisponível |
| RabbitMQ | ✅ funciona | ⚠️ dados defasados |

Nenhuma falha do lado da consolidação impede o registro de lançamentos, e nenhum
evento é perdido — apenas atrasado.

## Testes

TDD como fluxo de desenvolvimento, não como etapa posterior:
[`docs/testing-strategy.md`](./docs/testing-strategy.md) e
[ADR-008](./docs/decisions/ADR-008-tdd.md).

O pipeline de CI roda `restore → build → unitários → arquitetura → integração` em
todo Pull Request, e `master` só aceita merge com o pipeline verde. A garantia de
qualidade é uma propriedade do repositório, não uma promessa desta seção.

⏳ *Resultados e cobertura serão publicados aqui a partir da etapa 6.*

## Performance

Requisito: 50 chamadas/s com perda máxima de 5%.
Meta interna: erro HTTP < 1% e **perda de eventos igual a zero**.

Critérios e cenários: [ADR-010](./docs/decisions/ADR-010-performance-validation.md).

⏳ *Resultados medidos serão publicados aqui na etapa 13.*

## Observabilidade

Logs estruturados em JSON, correlation id propagado entre os quatro processos do
fluxo e health checks `live`/`ready`:
[ADR-011](./docs/decisions/ADR-011-observability.md).

## Trade-offs

| Escolha | Ganho | Custo aceito |
|---------|-------|--------------|
| Dois serviços em vez de um | Independência de falha | Mais infraestrutura e complexidade operacional |
| Mensageria assíncrona | Absorve pico, desacopla | Consistência eventual, debugging distribuído |
| Outbox | Zero perda de evento | Latência extra, tabela e worker adicionais |
| Bases separadas | Isolamento real de falha | Dado duplicado, sem JOIN entre contextos |
| Saldo pré-calculado | Leitura O(1) | Escrita mais cara, risco de divergência |
| TDD | Design testável, regressão barata | Ritmo inicial mais lento |
| Outbox e retry implementados à mão | Mecanismos visíveis e auditáveis | Mais código que usar MassTransit |

Cada custo está detalhado na ADR correspondente. Nenhuma peça da arquitetura existe
sem um requisito que a justifique — a
[matriz de rastreabilidade](./docs/requirements.md#5-matriz-de-rastreabilidade--requisito--decisão)
liga cada decisão ao requisito que a originou.

## Melhorias futuras

Itens conscientemente fora do MVP ([`docs/scope.md`](./docs/scope.md)):

- **Idempotency-Key** em `POST /transactions`, protegendo contra reenvio do cliente
- **Fuso horário do lojista** na definição do dia da consolidação (hoje: UTC)
- **Reconciliação periódica** entre lançamentos e saldo, detectando divergência
- **Expurgo** de `outbox_messages` e `processed_events` por política de retenção
- **OpenTelemetry + Jaeger + Prometheus** para tracing e métricas
- **Rotina de reprocessamento da DLQ** sem intervenção manual
- **Saldo acumulado** e consulta por período
- **Multi-tenant**, para atender múltiplos lojistas
- **Migrations como passo de deploy**, em vez de no startup da aplicação
- **CD e análise estática** no pipeline — a CI de build e testes entra já na
  etapa 5
