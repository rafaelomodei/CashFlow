# ADR-003 — RabbitMQ como broker de mensageria

- **Status:** Aceito
- **Data:** 2026-08-08
- **Complementado em:** 2026-08-09 — mecanismo de retry escolhido, como esta
  própria ADR previa para a etapa 10
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

O comportamento exigido é:

```
erro transitório → retry com espera → limite atingido → DLQ (em tempo finito)
```

#### Mecanismo escolhido (etapa 10)

**Retry in-process com espera limitada**, seguido de `nack(requeue=false)` para a
DLQ. Padrão: 3 tentativas com 2 segundos entre elas.

Ele foi escolhido por ser o único dos três que não acrescenta topologia. Fila de
retry com TTL exigiria uma fila por faixa de espera e um exchange a mais; ler
`x-death` exigiria interpretar um header cujo formato varia entre versões do
broker. Os dois resolvem o mesmo problema que este resolve, com mais peças para
manter e explicar.

O que o consumidor precisa provar são três coisas, e nenhuma delas depende do
mecanismo mais elaborado:

| Garantia | Como |
|----------|------|
| Mensagem não some | `ack` manual só depois do commit no banco |
| Mensagem repetida não duplica saldo | `processed_events` na mesma transação ([ADR-007](./ADR-007-idempotency.md)) |
| Mensagem problemática não trava a fila | Tentativas limitadas e DLQ |

O custo aceito: a mensagem ocupa uma vaga do `prefetch` durante a espera. Com 3
tentativas de 2 segundos, uma mensagem problemática segura uma vaga por até 6
segundos — irrelevante para a escala do desafio, e o motivo pelo qual as esperas
são curtas e o limite é baixo.

**Erro permanente não passa por retry.** Envelope ilegível, campo obrigatório
ausente e violação de regra de domínio vão direto para a DLQ: um JSON quebrado não
fica válido na segunda leitura, e um valor não positivo continua não positivo.
Retentá-los só atrasaria a fila.

#### Revisão (2026-08-10 — cenários de falha da etapa 12)

O destino ao esgotar as tentativas deixou de ser incondicionalmente a DLQ. O
cenário com o `consolidation_db` fora do ar por mais tempo que a janela de retry
mostrou o problema: eventos perfeitamente válidos iam para a DLQ e a recuperação
deixava de ser automática. A DLQ é para mensagem problemática, não para
infraestrutura indisponível.

O consumidor passou a distinguir os dois casos por `DbException.IsTransient`:
esgotadas as tentativas, falha de conectividade devolve a mensagem à fila
(`requeue=true`) para esperar o banco voltar; qualquer outra falha segue para a
DLQ. O laço quente que este documento registra acima não se reintroduz — o
requeue só acontece depois das tentativas com espera real, e a reentrega seguinte
repete o ciclo completo de retry.

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
