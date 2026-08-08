# ADR-011 — Observabilidade: logs estruturados, correlação e health checks

- **Status:** Aceito
- **Data:** 2026-08-08
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

### 1. Logs estruturados (Serilog)

Log em JSON, com propriedades consultáveis em vez de texto interpolado:

```json
{
  "timestamp": "2026-08-08T14:32:11.482Z",
  "level": "Information",
  "message": "Transaction registered",
  "correlationId": "b1f2...",
  "transactionId": "6c6a...",
  "type": "CREDIT",
  "amount": 1500.00,
  "service": "cashflow-api"
}
```

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
| Serilog + correlation + health checks | Baixo custo, alto retorno, sem infra extra | Sem métricas nem tracing visual | **Escolhida** |
| Stack completa OpenTelemetry + Jaeger + Prometheus + Grafana | Observabilidade de produção real | Quatro containers a mais para um sistema de dois serviços; desvia o foco da avaliação | Rejeitada — registrada como melhoria futura |
| Apenas `ILogger` padrão com texto | Zero configuração | Log não consultável; sem correlação | Rejeitada |
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
- Custo de infraestrutura zero.

**Negativas**

- Sem métricas agregadas nem dashboards: diagnóstico depende de leitura de logs.
- Sem tracing distribuído visual — a correlação é manual, por busca.
- Log estruturado é mais verboso em volume de bytes.

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
