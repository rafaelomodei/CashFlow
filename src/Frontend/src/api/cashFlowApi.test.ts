import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createTransaction, listTransactions } from './cashFlowApi';

const okJson = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });

const emptyPage = { items: [], nextCursor: null, hasMore: false };

describe('listTransactions', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue(okJson(emptyPage));
    vi.stubGlobal('fetch', fetchMock);
  });

  const calledUrl = (): string =>
    new URL(fetchMock.mock.calls[0]![0] as string, 'http://localhost').toString();

  it('calls the Cash Flow API through the same-origin proxy path', async () => {
    await listTransactions({});
    expect(calledUrl()).toContain('/api/cashflow/transactions');
  });

  it('omits filters that were not provided', async () => {
    await listTransactions({});
    const url = new URL(calledUrl());
    expect(url.searchParams.has('startDate')).toBe(false);
    expect(url.searchParams.has('endDate')).toBe(false);
    expect(url.searchParams.has('cursor')).toBe(false);
  });

  it('sends the period filter defined by the contract', async () => {
    await listTransactions({ startDate: '2026-08-01', endDate: '2026-08-09' });
    const url = new URL(calledUrl());
    expect(url.searchParams.get('startDate')).toBe('2026-08-01');
    expect(url.searchParams.get('endDate')).toBe('2026-08-09');
  });

  it('sends the cursor opaquely, exactly as it was received', async () => {
    const cursor = 'eyJvIjoiMjAyNi0wOC0wOFQwOToxNTowMFoifQ';
    await listTransactions({ cursor });
    expect(new URL(calledUrl()).searchParams.get('cursor')).toBe(cursor);
  });
});

describe('createTransaction', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue(
      okJson(
        {
          id: '6c6a2f4e-1b2c-4d3e-8f90-1a2b3c4d5e6f',
          type: 'CREDIT',
          amount: 1500,
          occurredAt: '2026-08-09T14:30:00Z',
          description: null,
          createdAt: '2026-08-09T14:32:11Z',
        },
        201,
      ),
    );
    vi.stubGlobal('fetch', fetchMock);
  });

  const sentBody = (): Record<string, unknown> =>
    JSON.parse((fetchMock.mock.calls[0]![1] as RequestInit).body as string);

  it('posts as JSON', async () => {
    await createTransaction({ type: 'CREDIT', amount: 1500 });
    const init = fetchMock.mock.calls[0]![1] as RequestInit;
    expect(init.method).toBe('POST');
    expect(new Headers(init.headers).get('Content-Type')).toContain('application/json');
  });

  it('sends amount as a number, never as a string', async () => {
    // §1.4 of the contract: money is a JSON number. A masked input that leaks
    // "1.500,00" through would be rejected as malformed, not as invalid.
    await createTransaction({ type: 'CREDIT', amount: 1500.5 });
    expect(sentBody().amount).toBe(1500.5);
  });

  it('omits occurredAt when it was not informed, letting the server decide', async () => {
    await createTransaction({ type: 'DEBIT', amount: 10 });
    expect('occurredAt' in sentBody()).toBe(false);
  });

  it('sends a civil date as an instant with an explicit offset', async () => {
    // The contract rejects an instant without offset. A date picker yields
    // `2026-08-09`, which is not an instant at all.
    await createTransaction({ type: 'CREDIT', amount: 10, occurredOn: '2026-08-09' });
    expect(sentBody().occurredAt).toBe('2026-08-09T00:00:00Z');
  });

  it('omits an empty description instead of sending an empty string', async () => {
    await createTransaction({ type: 'CREDIT', amount: 10, description: '   ' });
    expect('description' in sentBody()).toBe(false);
  });
});
