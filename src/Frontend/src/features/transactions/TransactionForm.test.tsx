import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TransactionForm } from './TransactionForm';
import { ApiError } from '../../api/http';

const defaults = {
  open: true,
  onClose: vi.fn(),
  onSubmit: vi.fn(),
  isSubmitting: false,
  error: null,
};

const typeAmount = async (value: string) => {
  await userEvent.type(screen.getByLabelText(/valor/i), value);
};

describe('TransactionForm', () => {
  it('does not call the API when the amount is not positive', async () => {
    const onSubmit = vi.fn();
    render(<TransactionForm {...defaults} onSubmit={onSubmit} />);

    await typeAmount('0');
    await userEvent.click(screen.getByRole('button', { name: /salvar/i }));

    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/maior que zero/i)).toBeInTheDocument();
  });

  it('keeps anything that is not a digit out of the amount field', async () => {
    render(<TransactionForm {...defaults} />);

    await typeAmount('abc');
    expect(screen.getByLabelText(/valor/i)).toHaveValue('');

    await typeAmount('1500,005');
    // The separators typed along the way are ignored: the digits are the amount.
    expect(screen.getByLabelText(/valor/i)).toHaveValue('15.000,05');
  });

  it('submits a valid draft with the amount as a number', async () => {
    const onSubmit = vi.fn();
    render(<TransactionForm {...defaults} onSubmit={onSubmit} />);

    await typeAmount('150050');
    expect(screen.getByLabelText(/valor/i)).toHaveValue('1.500,50');

    await userEvent.click(screen.getByRole('button', { name: /salvar/i }));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ type: 'CREDIT', amount: 1500.5 }),
    );
  });

  it('submits the selected type', async () => {
    const onSubmit = vi.fn();
    render(<TransactionForm {...defaults} onSubmit={onSubmit} />);

    await userEvent.selectOptions(screen.getByLabelText(/tipo/i), 'DEBIT');
    await typeAmount('200');
    await userEvent.click(screen.getByRole('button', { name: /salvar/i }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ type: 'DEBIT' }));
  });

  it('shows a 400 from the API on the field it belongs to', async () => {
    // The server is the authority. Whatever the client check let through comes
    // back as `errors` and has to land on the field, not in a generic banner.
    const error = new ApiError('One or more fields are invalid.', 400, {
      type: 'x',
      title: 'Validation failed',
      status: 400,
      detail: 'One or more fields are invalid.',
      errors: { amount: ['Amount must be greater than zero.'] },
    });

    render(<TransactionForm {...defaults} error={error} />);

    expect(screen.getByText('Amount must be greater than zero.')).toBeInTheDocument();
  });

  it('shows the correlation id when the failure was unexpected', async () => {
    const error = new ApiError('Falha inesperada.', 500, {
      type: 'x',
      title: 'Internal server error',
      status: 500,
      detail: 'Falha inesperada.',
      correlationId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
    });

    render(<TransactionForm {...defaults} error={error} />);

    expect(screen.getByText(/aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee/)).toBeInTheDocument();
  });

  it('blocks a second submission while the first is in flight', () => {
    render(<TransactionForm {...defaults} isSubmitting />);
    expect(screen.getByRole('button', { name: /salvando/i })).toBeDisabled();
  });
});
