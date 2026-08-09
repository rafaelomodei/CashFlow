# Testes de carga

Critérios e escopo em [ADR-010](../docs/decisions/ADR-010-performance-validation.md).

## Cenário obrigatório

```bash
docker compose up -d
docker compose --profile load run --rm k6 run /scripts/scenarios/read-daily-balance.js
```

50 req/s sustentados em `GET /daily-balances/{date}` por 30 segundos. O teste
**falha sozinho** se qualquer threshold for violado — o critério de aceite está
declarado no script, não na leitura do relatório.

| Threshold | Valor | Origem |
|-----------|-------|--------|
| `http_req_failed` | `< 1%` | Meta interna. O enunciado tolera 5% |
| `http_req_duration` p95 | `< 100 ms` | ADR-010 |
| `checks` | `> 99%` | Status e corpo conferidos a cada requisição |

Para medir contra um dia com movimentação, registre lançamentos antes e passe a
data:

```bash
curl -X POST http://localhost:5001/transactions \
  -H 'Content-Type: application/json' \
  -d '{"type":"CREDIT","amount":1500.00,"occurredAt":"2026-11-01T12:00:00Z"}'

docker compose --profile load run --rm -e BALANCE_DATE=2026-11-01 k6 \
  run /scripts/scenarios/read-daily-balance.js
```

## O que não é medido aqui

Perda zero de evento e independência de falha **não** são testes de carga. Elas são
provadas por teste funcional e pelos cenários executados em
[`architecture.md`](../docs/architecture.md) §6, onde a evidência é direta e não
depende da máquina de medição.

Carga de escrita (`POST /transactions`) e de ingestão (50 eventos/s) são extras de
ADR-010, executados apenas se sobrar tempo.

## Limitações

Os números são relativos ao ambiente local: tudo — banco, broker, aplicação e o
próprio k6 — compete pela mesma CPU. A afirmação que o projeto sustenta não é
"este sistema suporta 50 req/s em qualquer infraestrutura", e sim "o requisito foi
medido, nestas condições, com este resultado".
