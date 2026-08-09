import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { BalanceCard } from './BalanceCard';
import { ApiError } from '../../api/http';
import type { DailyBalance } from '../../api/types';

const now = new Date('2026-08-09T12:00:03Z');

const balance: DailyBalance = {
  date: '2026-08-09',
  totalCredits: 12300,
  totalDebits: 3760,
  balance: 8540,
  updatedAt: '2026-08-09T12:00:00Z',
};

const defaults = {
  date: '2026-08-09',
  onDateChange: vi.fn(),
  isLoading: false,
  error: null,
  onRetry: vi.fn(),
  phase: 'idle' as const,
  now,
};

describe('BalanceCard', () => {
  it('shows the three totals exactly as the API reported them', () => {
    render(<BalanceCard {...defaults} balance={balance} />);

    expect(screen.getByText('R$ 8.540,00')).toBeInTheDocument();
    expect(screen.getByText('R$ 12.300,00')).toBeInTheDocument();
    expect(screen.getByText('R$ 3.760,00')).toBeInTheDocument();
  });

  it('reports how long ago the consolidation was updated', () => {
    render(<BalanceCard {...defaults} balance={balance} />);
    expect(screen.getByText(/há 3 s/)).toBeInTheDocument();
  });

  it('shows a zeroed balance for a day with no movement, not an error', () => {
    // §3.1: a day without entries is a 200 with zeros and `updatedAt: null`.
    render(
      <BalanceCard
        {...defaults}
        balance={{
          date: '2026-08-10',
          totalCredits: 0,
          totalDebits: 0,
          balance: 0,
          updatedAt: null,
        }}
      />
    );

    expect(screen.getByTestId('balance-value')).toHaveTextContent('R$ 0,00');
    expect(screen.getByText(/sem movimenta/i)).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('renders a negative balance', () => {
    render(
      <BalanceCard
        {...defaults}
        balance={{ ...balance, balance: -800, totalCredits: 0, totalDebits: 800 }}
      />
    );
    expect(screen.getByTestId('balance-value')).toHaveTextContent('-R$ 800,00');
  });

  it('announces that it is syncing right after a registration', () => {
    render(<BalanceCard {...defaults} balance={balance} phase="syncing" />);
    expect(screen.getByText(/sincronizando/i)).toBeInTheDocument();
  });

  it('reports a late consolidation once the deadline passes', () => {
    render(<BalanceCard {...defaults} balance={balance} phase="stale" />);
    expect(screen.getByText(/atrasada/i)).toBeInTheDocument();
  });

  it('shows the error inside the card, with its correlation id', () => {
    const error = new ApiError('Falha inesperada.', 500, {
      type: 'x',
      title: 'Internal server error',
      status: 500,
      detail: 'Falha inesperada.',
      correlationId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
    });

    render(<BalanceCard {...defaults} error={error} />);

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText(/aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee/)).toBeInTheDocument();
  });
});
