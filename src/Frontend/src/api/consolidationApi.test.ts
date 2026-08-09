import { beforeEach, describe, expect, it, vi } from 'vitest';
import { getDailyBalance } from './consolidationApi';

describe('getDailyBalance', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          date: '2026-08-09',
          totalCredits: 1500,
          totalDebits: 700,
          balance: 800,
          updatedAt: '2026-08-09T14:32:15Z',
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    );
    vi.stubGlobal('fetch', fetchMock);
  });

  it('calls the Consolidation API through its own proxy path', async () => {
    await getDailyBalance('2026-08-09');
    expect(fetchMock.mock.calls[0]![0]).toContain('/api/consolidation/daily-balances/2026-08-09');
  });

  it('returns the balance as the API reported it', async () => {
    await expect(getDailyBalance('2026-08-09')).resolves.toEqual({
      date: '2026-08-09',
      totalCredits: 1500,
      totalDebits: 700,
      balance: 800,
      updatedAt: '2026-08-09T14:32:15Z',
    });
  });

  it('accepts a day with no movement as a valid zeroed balance', async () => {
    fetchMock.mockResolvedValue(
      new Response(
        JSON.stringify({
          date: '2026-08-10',
          totalCredits: 0,
          totalDebits: 0,
          balance: 0,
          updatedAt: null,
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    );

    const balance = await getDailyBalance('2026-08-10');

    expect(balance.balance).toBe(0);
    expect(balance.updatedAt).toBeNull();
  });
});
