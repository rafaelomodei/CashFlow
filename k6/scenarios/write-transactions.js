import http from 'k6/http';
import { check } from 'k6';

// Extra de ADR-010: a segunda leitura da ambiguidade — 50 lançamentos por segundo
// entrando no sistema. Serve também de gerador para a verificação de perda zero:
// o total registrado aqui tem de aparecer inteiro no saldo consolidado depois.

const BASE_URL = __ENV.CASHFLOW_URL || 'http://cashflow-api:8080';
const DATE = __ENV.TRANSACTION_DATE || '2026-11-02';
const AMOUNT = 1.00;

export const options = {
  scenarios: {
    write_transactions: {
      executor: 'constant-arrival-rate',
      rate: 50,
      timeUnit: '1s',
      duration: '30s',
      preAllocatedVUs: 20,
      maxVUs: 100,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    // Mais folga que na leitura: a escrita grava lançamento e evento na mesma
    // transação, e ainda assim não espera o broker (ADR-004).
    http_req_duration: ['p(95)<200'],
    checks: ['rate>0.99'],
  },
};

export default function () {
  const response = http.post(
    `${BASE_URL}/transactions`,
    JSON.stringify({ type: 'CREDIT', amount: AMOUNT, occurredAt: `${DATE}T12:00:00Z` }),
    { headers: { 'Content-Type': 'application/json' } },
  );

  check(response, {
    'status é 201': (r) => r.status === 201,
  });
}
