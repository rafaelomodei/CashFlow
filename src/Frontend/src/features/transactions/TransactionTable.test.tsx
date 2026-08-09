import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TransactionTable } from './TransactionTable';
import type { Transaction } from '../../api/types';

const items: Transaction[] = [
  {
    id: '1',
    type: 'CREDIT',
    amount: 500,
    occurredAt: '2026-08-09T14:30:00Z',
    description: 'Venda no balcão',
    createdAt: '2026-08-09T14:32:00Z',
  },
  {
    id: '2',
    type: 'DEBIT',
    amount: 200,
    occurredAt: '2026-08-09T09:15:00Z',
    description: null,
    createdAt: '2026-08-09T09:15:00Z',
  },
];

const defaults = {
  items: [] as Transaction[],
  isLoading: false,
  error: null,
  hasMore: false,
  isFetchingMore: false,
  onLoadMore: vi.fn(),
  onRetry: vi.fn(),
};

describe('TransactionTable', () => {
  it('lists each transaction with its type and amount', () => {
    render(<TransactionTable {...defaults} items={items} />);

    expect(screen.getByText('Venda no balcão')).toBeInTheDocument();
    expect(screen.getByText('Crédito')).toBeInTheDocument();
    expect(screen.getByText('Débito')).toBeInTheDocument();
    expect(screen.getByText('R$ 500,00')).toBeInTheDocument();
  });

  it('shows an empty state instead of an error for an empty page', () => {
    // §4.4: a period with no entries is a 200 with `items: []`. Rendering it as
    // "not found" would move a contract decision into the client.
    render(<TransactionTable {...defaults} items={[]} />);

    expect(screen.getByText(/nenhum lançamento/i)).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('offers "Carregar mais" only while there is a next cursor', () => {
    const { rerender } = render(<TransactionTable {...defaults} items={items} hasMore />);
    expect(screen.getByRole('button', { name: /carregar mais/i })).toBeInTheDocument();

    rerender(<TransactionTable {...defaults} items={items} hasMore={false} />);
    expect(screen.queryByRole('button', { name: /carregar mais/i })).not.toBeInTheDocument();
  });

  it('asks for the next page when "Carregar mais" is pressed', async () => {
    const onLoadMore = vi.fn();
    render(<TransactionTable {...defaults} items={items} hasMore onLoadMore={onLoadMore} />);

    await userEvent.click(screen.getByRole('button', { name: /carregar mais/i }));

    expect(onLoadMore).toHaveBeenCalledOnce();
  });

  it('never renders a total row — the contract has no totalCount', () => {
    render(<TransactionTable {...defaults} items={items} />);
    expect(screen.queryByText(/total/i)).not.toBeInTheDocument();
  });

  it('renders an entry with no description without showing "null"', () => {
    render(<TransactionTable {...defaults} items={items} />);
    expect(screen.queryByText('null')).not.toBeInTheDocument();
  });
});
