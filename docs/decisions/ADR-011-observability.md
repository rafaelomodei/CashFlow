# ADR-011 — Observabilidade: logs estruturados, correlação e health checks

- **Status:** Aceito
- **Data:** 2026-08-08
- **Revisado em:** 2026-08-09 — os três mecanismos permanecem; o log estruturado
  deixa de exigir Serilog. Ver [Revisão](#revisão-2026-08-09).
- **Decisores:** rafaelomodei

## Contexto

O enunciado pede atenção ao "manejo adequado de erros". A arquitetura escolhida
adiciona uma dificuldade concreta: um lançamento atravessa **quatro processos**
(API → outbox publisher → broker → worker) antes de virar saldo.

Quando o saldo não bate, a pergunta "onde parou?" precisa ter resposta objetiva.
Sem correlação entre os processos, a única alternativa seria inspecionar tabelas
manualmente — inviável e não demonstrável.

## Decisão

Três mecanismos, escolhidos pelo custo/benefício dentro do escopo:

### 1. Logs estruturados (`ILogger` + JSON console)

O `ILogger<T>` do próprio ASP.NET Core, com `AddJsonConsole()`. O que importa é a
**saída estruturada**, não a biblioteca que a produz — e o template de mensagem do
`ILogger` já preserva as propriedades:

```csharp
logger.LogInformation(
    "Transaction {TransactionId} registered as {Type} for {Amount}",
    transaction.Id, type, amount);
```

A saída é JSON por linha, com as propriedades do template preservadas em `State`
em vez de já interpoladas na mensagem:

```json
{
  "Timestamp": "2026-08-08T14:32:11.4820000+00:00",
  "LogLevel": "Information",
  "Category": "CashFlow.Api.TransactionsController",
  "Message": "Transaction 6c6a... registered as CREDIT for 1500.00",
  "State": {
    "TransactionId": "6c6a...",
    "Type": "CREDIT",
    "Amount": 1500.00,
    "{OriginalFormat}": "Transaction {TransactionId} registered as {Type} for {Amount}"
  },
  "Scopes": [{ "CorrelationId": "b1f2..." }]
}
```

O formato é o do próprio runtime — mais verboso que o de um Serilog configurado à
mão, e é o preço de não trazer a dependência. O `correlationId` viaja em escopo
(`ILogger.BeginScope`), e não como argumento repetido em cada chamada: assim ele
acompanha **todo** log da requisição, inclusive os que o framework emite.

Regras:

- Proibido logar dado sensível ou payload inteiro sem necessidade.
- Nível `Information` para eventos de negócio; `Warning` para retry; `Error` para
  falha que exige atenção; `Debug` fica fora de produção.
- Toda exceção capturada é logada com contexto suficiente para reproduzir.

### 2. Correlation ID propagado ponta a ponta

O identificador de correlação nasce na requisição HTTP (header `X-Correlation-Id`,
gerado se ausente) e é propagado:

```
HTTP request
    ↓ (X-Correlation-Id)
Cash Flow API  ──▶  outbox_messages.payload (no envelope do evento)
    ↓
RabbitMQ (header da mensagem)
    ↓
Consolidation Worker  ──▶  logs do worker
```

É isso que permite reconstruir a jornada completa de um lançamento com uma única
busca. Sem ele, os logs de quatro processos são quatro histórias desconexas.

### 3. Health checks

| Endpoint | Semântica |
|----------|-----------|
| `/health/live` | O processo está vivo |
| `/health/ready` | As dependências obrigatórias respondem |

Ponto arquitetural importante: a prontidão da **Cash Flow API não depende do
RabbitMQ**. Se o broker estiver fora, a API continua `ready`, porque o Outbox
mantém o registro de lançamentos funcionando corretamente
([ADR-004](./ADR-004-transactional-outbox.md)). Marcá-la como não-pronta faria um
orquestrador retirá-la de serviço — a própria instrumentação passaria a produzir a
indisponibilidade que RNF-001 pede para evitar.

A mesma regra vale um nível abaixo, na topologia do Compose: nenhum serviço declara
o `rabbitmq` como dependência de startup ([ADR-009](./ADR-009-containers.md)).
Health check e `depends_on` precisam contar a mesma história.

### Sinais mínimos monitorados

| Sinal | Por quê |
|-------|---------|
| Mensagens pendentes no outbox | Crescimento indica publisher travado |
| Profundidade da fila | Crescimento indica worker mais lento que a ingestão |
| Mensagens na DLQ | Qualquer valor > 0 exige investigação |
| Defasagem da consolidação (`now − updatedAt`) | Mede a janela de consistência eventual ([ADR-006](./ADR-006-consistency.md)) |

## Alternativas consideradas

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| `ILogger` + `AddJsonConsole` + correlation + health checks | Saída estruturada sem dependência externa; já vem no runtime | Menos recursos de *sink* que Serilog | **Escolhida** |
| Serilog + correlation + health checks | Ecossistema de sinks maduro, enrichers prontos | Dependência externa para um ganho que este escopo não usa: um único sink, console | Rejeitada — era a escolha original, revista em 2026-08-09 |
| Stack completa OpenTelemetry + Jaeger + Prometheus + Grafana | Observabilidade de produção real | Quatro containers a mais para um sistema de dois serviços; desvia o foco da avaliação | Rejeitada — registrada como melhoria futura |
| `ILogger` com saída de **texto** | Zero configuração | Log não consultável; sem correlação | Rejeitada |
| APM comercial (Datadog, New Relic) | Completo | Dependência externa; quebra a execução local autônoma | Rejeitada |

Instrumentação **OpenTelemetry-ready** é adotada onde não custa nada (uso de
`Activity`/`ActivitySource` do .NET), de modo que exportar traces no futuro seja
configuração e não reescrita.

## Consequências

**Positivas**

- A jornada de um lançamento é rastreável entre os quatro processos.
- Os sinais monitorados tornam visível a saúde dos pontos que as outras ADRs criaram.
- Health checks viabilizam `depends_on: service_healthy` no Compose para as
  dependências **obrigatórias** ([ADR-009](./ADR-009-containers.md)).
- Custo de infraestrutura zero, e agora também zero dependência de log.

**Negativas**

- Sem métricas agregadas nem dashboards: diagnóstico depende de leitura de logs.
- Sem tracing distribuído visual — a correlação é manual, por busca.
- Log estruturado é mais verboso em volume de bytes.
- `AddJsonConsole` tem um único destino, o console. Enviar log para arquivo,
  Seq ou Elasticsearch exigiria trazer Serilog de volta — barato, porque o código
  que chama `ILogger` não mudaria.

## Trade-off aceito

Escolhemos **o mínimo que responde às perguntas certas** em vez da stack completa
de observabilidade. Correlation ID resolve 80% do diagnóstico distribuído a um
custo próximo de zero; Prometheus e Jaeger resolveriam os 20% restantes ao custo de
quadruplicar a infraestrutura de um sistema com dois serviços.

Essa decisão é revisável: com mais serviços ou operação real, tracing distribuído
passaria a se pagar.

## Requisitos atendidos

RNF-011, RNF-013

## Como validar

- Registrar um lançamento e localizar, pelo `correlationId`, os logs da API, do
  publisher e do worker.
- Derrubar o RabbitMQ e verificar que `/health/ready` da Cash Flow API segue `200`.
- Derrubar o `cashflow-db` e verificar que `/health/ready` da mesma API passa a falhar.
- Conferir que a saída de `docker compose logs cashflow-api` é JSON por linha, com
  as propriedades do template como campos — e não texto já interpolado.

---

## Revisão (2026-08-09)

**O que mudou:** o log estruturado passa a usar `ILogger` com `AddJsonConsole()`,
em vez de Serilog. Correlation ID e health checks permanecem exatamente como
estavam.

**Por quê:** a decisão real desta ADR sempre foi *log estruturado com propriedades
consultáveis*, e o Serilog entrou como o meio óbvio de chegar lá. Só que o
`ILogger` do runtime já entrega isso: o template de mensagem preserva as
propriedades e `AddJsonConsole` as serializa. O que o Serilog acrescentaria — o
ecossistema de sinks — é justamente o que este escopo não usa, porque há um único
destino, o console.

A linha "apenas `ILogger` padrão com texto" da tabela de alternativas rejeitava a
**saída em texto**, não o `ILogger`. A revisão corrige essa imprecisão: eram duas
opções diferentes tratadas como uma.

**Custo de reverter:** baixo, e essa é parte da justificativa. Trocar para Serilog
depois é configuração no *composition root*; nenhuma chamada a `ILogger` no código
de aplicação muda.

**O que não mudou:** os três mecanismos da decisão, as regras de nível e de dado
sensível, a proibição de o `ready` da Cash Flow API depender do broker, e os sinais
mínimos monitorados.
