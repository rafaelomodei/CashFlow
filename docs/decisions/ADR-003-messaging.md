# ADR-003 — RabbitMQ como broker de mensageria

- **Status:** Aceito
- **Data:** 2026-08-08
- **Decisores:** rafaelomodei

## Contexto

[ADR-002](./ADR-002-service-decomposition.md) definiu que a comunicação entre
lançamentos e consolidação é assíncrona. Falta decidir **por qual meio**.

As forças em jogo:

- RNF-003: pico de 50 chamadas/s na consolidação.
- RNF-004: perda máxima de 5% — ou seja, entrega precisa ser confiável.
- RNF-005: falha temporária da consolidação não pode derrubar o fluxo principal.
- RNF-012: o ambiente inteiro precisa subir localmente de forma previsível.

## Decisão

Usamos **RabbitMQ** como broker, com a seguinte topologia:

```
exchange: cashflow.transactions   (tipo: topic, durable)
    │
    └── routing key: transaction.registered
            │
            ▼
    queue: consolidation.transaction-registered  (durable)
            │  (x-dead-letter-exchange)
            ▼
    exchange: cashflow.transactions.dlx
            │
            ▼
    queue: consolidation.transaction-registered.dlq
```

Configurações que sustentam RNF-004:

| Configuração | Valor | Por quê |
|--------------|-------|---------|
| Mensagens persistentes | `delivery_mode=2` | Sobrevivem a restart do broker |
| Fila e exchange | `durable=true` | Sobrevivem a restart do broker |
| Confirmação de publicação | *publisher confirms* | O outbox só marca como publicado após o ack do broker |
| Confirmação de consumo | ack **manual**, após commit no banco | Mensagem só sai da fila depois do efeito persistido |
| Retry | tentativas limitadas, com **espera real** entre elas | Absorve falha transitória do banco sem laço quente — ver abaixo |
| Após o limite | mensagem vai para a **DLQ** | Erro permanente não bloqueia a fila (evita *poison message*) |
| `prefetch` | ajustado (ex.: 20) | Controla concorrência e evita sobrecarregar o consumidor |
| Versão | série **4.x** | Série com suporte comunitário ativo; imagem fixada em [ADR-009](./ADR-009-containers.md) |

A fila também atua como **buffer**: se chegarem 50 eventos/s e o worker consumir
menos, a fila cresce e drena depois. O pico não vira erro, vira latência.

### Retry: o que o RabbitMQ *não* faz sozinho

Vale registrar explicitamente, porque é um erro comum: o RabbitMQ não implementa
backoff. `basic.nack` com `requeue=true` devolve a mensagem à fila e ela é
reentregue imediatamente, produzindo um laço quente:

```
consome → falha → requeue → consome → falha → requeue → ...
```

Isso queima CPU, polui o log e nunca atinge o limite de tentativas de forma útil.
O backoff precisa vir de um mecanismo explícito. Três caminhos viáveis:

| Mecanismo | Como funciona | Custo |
|-----------|---------------|-------|
| Fila de retry com TTL + DLX | `nack(requeue=false)` envia para uma fila com `x-message-ttl`, que ao expirar devolve à fila principal; uma fila por faixa de espera dá o escalonamento | Mais topologia, backoff em degraus |
| Contagem via header `x-death` | O próprio broker registra as passagens por dead-lettering; o consumidor lê a contagem e decide entre nova espera e DLQ | Depende do formato do header |
| Retry in-process antes do `nack` | Espera limitada dentro do consumidor (ex.: 3 tentativas curtas); esgotado, `nack(requeue=false)` direto para a DLQ | Segura a mensagem e o `prefetch` durante a espera |

A escolha entre eles é feita na **etapa 10** do [roadmap](../roadmap.md), quando o
consumidor for implementado, e o motivo é registrado aqui. O que já está decidido
agora é o comportamento exigido:

```
erro transitório → retry com espera → limite atingido → DLQ (em tempo finito)
```

## Alternativas consideradas

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| RabbitMQ | Maduro, ack manual, DLQ nativa, imagem Docker leve, ferramental .NET consolidado | Não é log persistente; sem replay histórico após ack | **Escolhida** |
| Apache Kafka | Alta vazão, retenção e replay, particionamento | Complexidade operacional muito acima da necessidade; 50 req/s é irrelevante para o Kafka; ambiente local mais pesado | Rejeitada — over-engineering |
| Azure Service Bus / AWS SQS | Gerenciado, confiável | Dependência de nuvem quebra RNF-012 (execução local previsível) | Rejeitada |
| Redis Streams | Leve, rápido | Garantias de durabilidade mais frágeis para o papel de broker de negócio | Rejeitada |
| Chamada HTTP direta com retry | Sem infra extra | Acoplamento temporal; contraria RNF-001 | Rejeitada |
| Tabela de fila no próprio banco (*polling*) | Zero infra extra | Reintroduz acoplamento entre contextos; o enunciado sugere mensageria como diferencial | Rejeitada |

## Consequências

**Positivas**

- Desacoplamento temporal: produtor e consumidor não precisam estar vivos juntos.
- A fila absorve picos, transformando sobrecarga em latência em vez de erro.
- DLQ separa falha transitória de falha permanente.
- Atende ao requisito **opcional** do enunciado sobre uso de mensageria.

**Negativas**

- Mais um componente de infraestrutura para operar e monitorar.
- Entrega *at-least-once*: exige idempotência no consumidor ([ADR-007](./ADR-007-idempotency.md)).
- Ordem entre mensagens não é garantida sob concorrência — aceitável porque a
  consolidação é uma soma (operação comutativa).
- Mensagens na DLQ exigem intervenção manual ou uma rotina de reprocessamento.

## Trade-off aceito

Escolhemos **at-least-once com idempotência** em vez de perseguir
*exactly-once*. Entrega exatamente-uma-vez fim a fim não existe de forma barata
em sistemas distribuídos; o par "at-least-once + consumidor idempotente" produz o
mesmo resultado observável com muito menos complexidade.

Também aceitamos **não ter replay histórico** (o que o Kafka daria). Para este
domínio, o `outbox` e a tabela de lançamentos são a fonte da verdade, e uma
reconstrução completa do saldo pode ser feita a partir deles.

## Requisitos atendidos

RNF-003, RNF-004, RNF-005, RNF-006, RNF-011

## Como validar

- Teste de integração com Testcontainers: publica evento, worker consome, saldo atualiza.
- Teste de resiliência: derrubar o broker durante a carga e verificar que nenhum
  evento é perdido após ele voltar.
- Teste de *poison message*: evento inválido deve terminar na DLQ sem travar a fila.
- Teste de backoff: uma mensagem que falha sempre atinge a DLQ em tempo finito e
  com número limitado de entregas — sem laço de reentrega imediata.
