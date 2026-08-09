import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DashboardPage } from './DashboardPage';
import { renderWithClient } from '../test/renderWithClient';

const json = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });

const problem = (body: unknown, status: number): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });

const emptyPage = { items: [], nextCursor: null, hasMore: false };

const balance = {
  date: '2026-08-09',
  totalCredits: 1500,
  totalDebits: 700,
  balance: 800,
  updatedAt: '2026-08-09T14:32:15Z',
};

const created = {
  id: '6c6a2f4e-1b2c-4d3e-8f90-1a2b3c4d5e6f',
  type: 'CREDIT',
  amount: 1500,
  occurredAt: '2026-08-09T14:30:00Z',
  description: null,
  createdAt: '2026-08-09T14:32:11Z',
};

/** Routes each stubbed call by the proxy path, the way nginx does. */
const routeFetch = (handlers: {
  transactions?: () => Response;
  createTransaction?: () => Response;
  dailyBalance?: () => Response;
}) =>
  vi.fn(async (url: string, init?: RequestInit) => {
    if (url.includes('/api/consolidation/daily-balances')) {
      return handlers.dailyBalance?.() ?? json(balance);
    }
    if (url.includes('/api/cashflow/transactions')) {
      if (init?.method === 'POST') {
        return handlers.createTransaction?.() ?? json(created, 201);
      }
      return handlers.transactions?.() ?? json(emptyPage);
    }
    throw new Error(`unexpected request to ${url}`);
  });

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', routeFetch({}));
  });

  it('shows the balance and the transactions', async () => {
    vi.stubGlobal(
      'fetch',
      routeFetch({
        transactions: () =>
          json({
            items: [
              {
                id: '1',
                type: 'CREDIT',
                amount: 500,
                occurredAt: '2026-08-09T14:30:00Z',
                description: 'Venda no balcão',
                createdAt: '2026-08-09T14:32:00Z',
              },
            ],
            nextCursor: null,
            hasMore: false,
          }),
      }),
    );

    renderWithClient(<DashboardPage />);

    expect(await screen.findByText('R$ 800,00')).toBeInTheDocument();
    expect(await screen.findByText('Venda no balcão')).toBeInTheDocument();
  });

  it('keeps the transaction form working while the Consolidation API is down', async () => {
    // This is RNF-001 on screen: the two contexts fail independently, so half
    // the page breaking must not take the other half with it.
    const fetchMock = routeFetch({
      dailyBalance: () =>
        problem(
          {
            type: 'https://cashflow.dev/problems/internal-error',
            title: 'Internal server error',
            status: 500,
            detail: 'An unexpected error occurred.',
            correlationId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
          },
          500,
        ),
    });
    vi.stubGlobal('fetch', fetchMock);

    renderWithClient(<DashboardPage />);

    expect(await screen.findByRole('alert')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /novo lançamento/i }));
    await userEvent.type(screen.getByLabelText(/valor/i), '1500');
    await userEvent.click(screen.getByRole('button', { name: /salvar/i }));

    await waitFor(() => {
      const posted = fetchMock.mock.calls.filter(
        ([, init]) => (init as RequestInit | undefined)?.method === 'POST',
      );
      expect(posted).toHaveLength(1);
    });
  });

  it('refreshes the list after a successful registration', async () => {
    const fetchMock = routeFetch({});
    vi.stubGlobal('fetch', fetchMock);

    renderWithClient(<DashboardPage />);
    await screen.findByText(/nenhum lançamento/i);

    const listCallsBefore = fetchMock.mock.calls.filter(
      ([url, init]) =>
        String(url).includes('/api/cashflow/transactions') &&
        (init as RequestInit | undefined)?.method !== 'POST',
    ).length;

    await userEvent.click(screen.getByRole('button', { name: /novo lançamento/i }));
    await userEvent.type(screen.getByLabelText(/valor/i), '1500');
    await userEvent.click(screen.getByRole('button', { name: /salvar/i }));

    await waitFor(() => {
      const listCallsAfter = fetchMock.mock.calls.filter(
        ([url, init]) =>
          String(url).includes('/api/cashflow/transactions') &&
          (init as RequestInit | undefined)?.method !== 'POST',
      ).length;
      expect(listCallsAfter).toBeGreaterThan(listCallsBefore);
    });
  });

  it('closes the modal once the registration succeeds', async () => {
    renderWithClient(<DashboardPage />);
    await screen.findByText(/nenhum lançamento/i);

    await userEvent.click(screen.getByRole('button', { name: /novo lançamento/i }));
    await userEvent.type(screen.getByLabelText(/valor/i), '1500');
    await userEvent.click(screen.getByRole('button', { name: /salvar/i }));

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
  });

  it('keeps the modal open when the registration fails', async () => {
    vi.stubGlobal(
      'fetch',
      routeFetch({
        createTransaction: () =>
          problem(
            {
              type: 'https://cashflow.dev/problems/validation-error',
              title: 'Validation failed',
              status: 400,
              detail: 'One or more fields are invalid.',
              errors: { amount: ['Amount must be greater than zero.'] },
            },
            400,
          ),
      }),
    );

    renderWithClient(<DashboardPage />);
    await screen.findByText(/nenhum lançamento/i);

    await userEvent.click(screen.getByRole('button', { name: /novo lançamento/i }));
    await userEvent.type(screen.getByLabelText(/valor/i), '1500');
    await userEvent.click(screen.getByRole('button', { name: /salvar/i }));

    // Closing on failure would discard what the user typed and hide the reason.
    expect(await screen.findByText('Amount must be greater than zero.')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});
