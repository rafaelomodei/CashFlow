import { request } from './http';
import type {
  CreateTransactionInput,
  ListTransactionsQuery,
  Transaction,
  TransactionPage,
} from './types';

/**
 * Client for the Cash Flow bounded context.
 *
 * The base path is relative on purpose: nginx (production) and the Vite dev
 * server both expose the API under `/api/cashflow`, so no service address ever
 * reaches the bundle. This module knows nothing about the Consolidation API,
 * and never will — the two contexts stay separate here too.
 */
const BASE = '/api/cashflow';

export async function listTransactions(query: ListTransactionsQuery): Promise<TransactionPage> {
  const params = new URLSearchParams();
  if (query.limit !== undefined) params.set('limit', String(query.limit));
  if (query.cursor) params.set('cursor', query.cursor);
  if (query.startDate) params.set('startDate', query.startDate);
  if (query.endDate) params.set('endDate', query.endDate);

  const qs = params.toString();
  return request<TransactionPage>(`${BASE}/transactions${qs ? `?${qs}` : ''}`);
}

export async function getTransaction(id: string): Promise<Transaction> {
  return request<Transaction>(`${BASE}/transactions/${id}`);
}

export async function createTransaction(input: CreateTransactionInput): Promise<Transaction> {
  const body: Record<string, unknown> = {
    type: input.type,
    amount: input.amount,
  };

  // A civil date is not an instant. The contract rejects `2026-08-09` and also
  // rejects an instant without offset, so the midnight-UTC reading is made
  // explicit here instead of being guessed by the server.
  if (input.occurredOn) {
    body.occurredAt = `${input.occurredOn}T00:00:00Z`;
  }

  // The contract treats `""` and `null` as absent (§2.1). Sending whitespace
  // would persist a description that renders as an empty cell.
  const description = input.description?.trim();
  if (description) {
    body.description = description;
  }

  return request<Transaction>(`${BASE}/transactions`, {
    method: 'POST',
    body: JSON.stringify(body),
  });
}
