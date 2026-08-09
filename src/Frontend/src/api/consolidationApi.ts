import { request } from './http';
import type { DailyBalance } from './types';

/**
 * Client for the Consolidation bounded context.
 *
 * Separate from `cashFlowApi` and unaware of it. The two never share a client,
 * a base URL or an error path — that separation is what lets one of them fail
 * while the other keeps working, which is the behaviour the screen exists to
 * show (ADR-002, RNF-001).
 */
const BASE = '/api/consolidation';

export async function getDailyBalance(date: string): Promise<DailyBalance> {
  return request<DailyBalance>(`${BASE}/daily-balances/${date}`);
}
