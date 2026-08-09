import { useInfiniteQuery } from '@tanstack/react-query';
import { listTransactions } from '../../api/cashFlowApi';

export interface PeriodFilter {
  startDate?: string;
  endDate?: string;
}

export const transactionsKey = (filter: PeriodFilter) => ['transactions', filter] as const;

const PAGE_SIZE = 20;

/**
 * Cursor pagination, as ADR-014 defined it. The cursor is opaque: it is handed
 * back exactly as received, never parsed or built here. The filters travel on
 * every request because the cursor encodes position only.
 */
export function useTransactions(filter: PeriodFilter) {
  return useInfiniteQuery({
    queryKey: transactionsKey(filter),
    queryFn: ({ pageParam }) =>
      listTransactions({ ...filter, cursor: pageParam, limit: PAGE_SIZE }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  });
}
