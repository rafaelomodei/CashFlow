# Documentação — CashFlow

Documentação do desafio técnico de desenvolvimento de software.
Solução para gestão de fluxo de caixa de um lojista, com lançamentos de crédito e
débito e relatório de saldo diário consolidado.

## Índice

| Documento | O que responde |
|-----------|----------------|
| [`requirements.md`](./requirements.md) | O que o sistema precisa fazer e por quê — RF, RNF, restrições, premissas e rastreabilidade |
| [`scope.md`](./scope.md) | O que está e o que **não** está no escopo, com justificativa |
| [`architecture.md`](./architecture.md) | Como o sistema é estruturado — diagramas, fluxos, comportamento sob falha |
| [`api-contracts.md`](./api-contracts.md) | Qual é o contrato exato — endpoints, DTOs, erros e o schema do evento |
| [`decisions/`](./decisions/README.md) | Por que cada escolha foi feita — 15 ADRs com alternativas e trade-offs |
| [`testing-strategy.md`](./testing-strategy.md) | Como a corretude é garantida — TDD, níveis e plano de testes |
| [`roadmap.md`](./roadmap.md) | Em que ordem o projeto é construído e por que nesta ordem |
| [`progress.md`](./progress.md) | Onde a execução está agora — checklist detalhado e próximo item |
| [`challenge/`](./challenge/) | Enunciado original do desafio |

> `roadmap.md` é a visão estratégica; `progress.md` é o backlog executável
> derivado dela. Quem quer entender a arquitetura lê o primeiro; quem vai
> implementar o próximo item lê o segundo.

## Por onde começar

**Para avaliar as decisões técnicas:**
[`architecture.md`](./architecture.md) → [`decisions/README.md`](./decisions/README.md)

**Para integrar com as APIs ou consumir o evento:**
[`api-contracts.md`](./api-contracts.md)

**Para entender o recorte do problema:**
[`requirements.md`](./requirements.md) → [`scope.md`](./scope.md)

**Para executar o projeto:**
[`../README.md`](../README.md)

## Resumo da solução em um parágrafo

Dois serviços independentes: um registra lançamentos financeiros, outro fornece o
saldo diário consolidado. Eles não se comunicam por HTTP — o serviço de lançamentos
grava o evento em uma tabela de **outbox**, na mesma transação do lançamento, e um
publisher assíncrono o envia ao **RabbitMQ**. Um worker consome, aplica de forma
**idempotente** ao saldo do dia e o expõe por uma API própria, com banco próprio.
O resultado é que a consolidação inteira pode estar fora do ar sem impedir um único
lançamento, e nenhum evento é perdido — apenas atrasado.

## Estado atual

Sistema **implementado e validado**: os dois serviços, o worker, o outbox, o
consumo idempotente, as APIs HTTP, o frontend de demonstração, os cenários de
falha executados e os testes de carga medidos. O que resta é a revisão final da
etapa 14 (README e conferência da documentação contra o entregue).

Acompanhamento item a item: [`progress.md`](./progress.md).
