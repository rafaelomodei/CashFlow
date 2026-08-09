import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createTransaction } from '../../api/cashFlowApi';
import { dailyBalanceKey } from '../daily-balance/useDailyBalance';
import { utcDayOf } from '../../lib/format';
import type { CreateTransactionInput, Transaction } from '../../api/types';

interface Options {
  /** Called with the created transaction and the day it consolidates into. */
  onRegistered: (created: Transaction, affectedDay: string) => void;
}

/**
 * Registering a transaction and reacting to the `201`.
 *
 * The list is invalidated because the transaction *is* registered — that half is
 * strongly consistent. The balance is invalidated too, but the refetch it
 * triggers will usually still show the old number: the worker has not applied
 * the event yet. That gap is the point, and the caller turns it into the
 * "sincronizando" state rather than hiding it behind a spinner.
 */
export function useCreateTransaction({ onRegistered }: Options) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: CreateTransactionInput) => createTransaction(input),
    onSuccess: (created) => {
      const affectedDay = utcDayOf(created.occurredAt);

      queryClient.invalidateQueries({ queryKey: ['transactions'] });
      queryClient.invalidateQueries({ queryKey: dailyBalanceKey(affectedDay) });

      onRegistered(created, affectedDay);
    },
  });
}
