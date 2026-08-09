import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, request } from './http';

const jsonResponse = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });

const problemResponse = (body: unknown, status: number): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });

describe('request', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });

  it('sends a correlation id on every request', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ ok: true }));

    await request('/api/cashflow/transactions');

    const init = fetchMock.mock.calls[0]![1] as RequestInit;
    const headers = new Headers(init.headers);
    expect(headers.get('X-Correlation-Id')).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
    );
  });

  it('parses a successful JSON body', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ balance: 800 }));

    await expect(request('/api/consolidation/daily-balances/2026-08-09')).resolves.toEqual({
      balance: 800,
    });
  });

  it('exposes field errors from a 400 Problem Details response', async () => {
    fetchMock.mockResolvedValue(
      problemResponse(
        {
          type: 'https://cashflow.dev/problems/validation-error',
          title: 'Validation failed',
          status: 400,
          detail: 'One or more fields are invalid.',
          correlationId: 'b1f2c3d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d',
          errors: {
            amount: ['Amount must be greater than zero.'],
            type: ['Type must be either CREDIT or DEBIT.'],
          },
        },
        400,
      ),
    );

    const error = await request('/api/cashflow/transactions').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
    expect((error as ApiError).fieldErrors).toEqual({
      amount: ['Amount must be greater than zero.'],
      type: ['Type must be either CREDIT or DEBIT.'],
    });
  });

  it('keeps the correlation id of a 500 so the failure stays traceable', async () => {
    fetchMock.mockResolvedValue(
      problemResponse(
        {
          type: 'https://cashflow.dev/problems/internal-error',
          title: 'Internal server error',
          status: 500,
          detail: 'An unexpected error occurred. Use the correlationId to trace it.',
          correlationId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
        },
        500,
      ),
    );

    const error = (await request('/api/cashflow/transactions').catch(
      (e: unknown) => e,
    )) as ApiError;

    expect(error.correlationId).toBe('aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee');
    expect(error.fieldErrors).toBeUndefined();
  });

  it('survives an error response that is not Problem Details', async () => {
    // nginx answers 502 in plain HTML when an API is down. Trying to read
    // `errors` off that would replace the real failure with a parse error.
    fetchMock.mockResolvedValue(new Response('<html>502 Bad Gateway</html>', { status: 502 }));

    const error = (await request('/api/cashflow/transactions').catch(
      (e: unknown) => e,
    )) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(502);
  });

  it('reports a network failure as an ApiError instead of leaking TypeError', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

    const error = (await request('/api/cashflow/transactions').catch(
      (e: unknown) => e,
    )) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(0);
  });

  it('returns undefined for a 204 with no body', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }));

    await expect(request('/api/cashflow/transactions')).resolves.toBeUndefined();
  });
});
