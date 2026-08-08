# ADR-002 — Dois serviços independentes: Lançamentos e Consolidação

- **Status:** Aceito
- **Data:** 2026-08-08
- **Decisores:** rafaelomodei

## Contexto

O enunciado descreve dois requisitos de negócio separados ("uma aplicação focada
na gestão dos lançamentos" e "uma aplicação responsável por fornecer o saldo
diário consolidado") e um requisito não funcional decisivo:

> "A aplicação de gestão de lançamentos precisa continuar operante mesmo em caso
> de falha no sistema de consolidação diária."

O enunciado não delimita o **domínio** dessa falha, e é aí que está a decisão. Um
processo único com banco único atende ao requisito quando a falha é lógica (um bug
na rotina de consolidação, por exemplo), mas não quando ela é de processo ou de
infraestrutura: se o banco cai, tudo cai; se um módulo consome todo o pool de
conexões, o outro degrada junto.

A pergunta que esta ADR responde não é "qual arquitetura atende ao enunciado", e
sim **que nível de isolamento decidimos sustentar e demonstrar**.

## Decisão

Separamos o sistema em **dois contextos independentes**, cada um com seu próprio
processo, seu próprio banco e seu próprio ciclo de vida:

| Contexto | Componentes | Responsabilidade |
|----------|-------------|------------------|
| **Cash Flow** | API + `cashflow_db` + Outbox Publisher | Registrar e consultar lançamentos |
| **Consolidation** | Worker + API + `consolidation_db` | Consolidar e expor o saldo diário |

Restrições que tornam a independência real:

1. **Nenhuma chamada HTTP síncrona** entre os dois contextos.
2. **Nenhum banco compartilhado** — ver [ADR-005](./ADR-005-database.md).
3. Comunicação exclusivamente por **eventos assíncronos** — ver [ADR-003](./ADR-003-messaging.md).
4. O único código compartilhado é `Shared.Contracts`, com os contratos de evento —
   sem regra de negócio.

## Alternativas consideradas

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| Monolito único com dois módulos | Muito mais simples, transação local, sem infra extra | Isolamento limitado a falhas lógicas; falha de processo ou de banco derruba os dois lados | Rejeitada |
| Monolito modular com bancos separados | Fronteira lógica clara, menos infra | Ainda é um processo só: falha de processo derruba os dois; deploy acoplado | Rejeitada, mas seria a escolha certa se RNF-001 não existisse |
| Dois serviços com chamada HTTP síncrona | Consistência imediata, simples de entender | Acoplamento temporal: com a consolidação fora do ar, o `POST /transactions` falharia ou dependeria de fallback — contraria RNF-001 no cenário que o enunciado descreve | Rejeitada |
| Dois serviços com comunicação assíncrona | Independência real de falha, absorve pico | Consistência eventual, mais componentes | **Escolhida** |
| Microsserviços granulares (um por caso de uso) | Escala independente máxima | Complexidade injustificável neste escopo | Rejeitada |

## Consequências

**Positivas**

- RNF-001 vira propriedade estrutural, não promessa: derrubar todo o contexto de
  consolidação e ver `POST /transactions` retornar `201` é um teste executável.
- Cada lado escala de forma independente (o de leitura tende a escalar diferente
  do de escrita).
- Fronteira de contexto explícita, no espírito de DDD.

**Negativas**

- Dois bancos, dois processos, um broker: mais infraestrutura para operar.
- Consistência eventual entre lançamento e saldo ([ADR-006](./ADR-006-consistency.md)).
- Debugging distribuído — mitigado por correlation id ([ADR-011](./ADR-011-observability.md)).
- Dado duplicado: o valor do lançamento existe nos dois lados.

## Trade-off aceito

Assumimos **complexidade operacional** para obter **independência de falha**. Esta
é a troca central do projeto, e ela é feita porque o enunciado coloca a
independência como requisito — não por preferência arquitetural. Não afirmamos que
esta seja a única arquitetura capaz de atender ao enunciado: afirmamos que é a que
escolhemos, pelo nível de isolamento que oferece e com os custos acima aceitos.
Removido o requisito de independência, um monolito modular seria a resposta correta
e a arquitetura deveria ser simplificada.

## Requisitos atendidos

RNF-001, RNF-002, RNF-005, RF-006

## Como validar

Teste de resiliência documentado no README:

```bash
docker compose stop consolidation-api consolidation-worker consolidation-db rabbitmq
curl -X POST localhost:5001/transactions -d '{...}'   # deve responder 201
docker compose start consolidation-api consolidation-worker consolidation-db rabbitmq
curl localhost:5002/daily-balances/2026-08-08          # saldo converge sozinho
```
