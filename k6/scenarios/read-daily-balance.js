import http from 'k6/http';
import { check } from 'k6';

// Cenário obrigatório de ADR-010: leitura do saldo consolidado sob carga.
//
// "O sistema de consolidação chega a processar 50 chamadas por segundo" é a frase
// ambígua do enunciado. A interpretação principal adotada é esta — leitura do
// saldo —, porque a frase fala do sistema de consolidação. As outras leituras
// estão registradas no README e ficam como extras.

const BASE_URL = __ENV.CONSOLIDATION_URL || 'http://consolidation-api:8080';
const DATE = __ENV.BALANCE_DATE || '2026-11-01';

export const options = {
  scenarios: {
    read_daily_balance: {
      executor: 'constant-arrival-rate',
      // Taxa fixa, e não VUs fixos: o requisito é sobre chamadas por segundo, e
      // com VUs fixos a taxa cairia sozinha se o servidor ficasse mais lento —
      // o teste passaria justamente quando o sistema piorasse.
      rate: 50,
      timeUnit: '1s',
      duration: '30s',
      preAllocatedVUs: 20,
      maxVUs: 100,
    },
  },
  thresholds: {
    // O enunciado tolera 5% de perda; a meta interna é bem menor, para que o
    // critério oficial seja atingido com folga em máquinas mais modestas.
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<100'],
    checks: ['rate>0.99'],
  },
};

export default function () {
  const response = http.get(`${BASE_URL}/daily-balances/${DATE}`);

  check(response, {
    'status é 200': (r) => r.status === 200,
    // Um dia sem movimentação responde 200 com saldo zero, nunca 404 (ADR-006):
    // conferir o corpo evita que o teste passe com uma resposta vazia.
    'corpo traz o saldo': (r) => r.json('balance') !== undefined,
  });
}
