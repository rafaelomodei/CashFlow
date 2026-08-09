import { useState, type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ApiError } from '../api/http';

/**
 * A 4xx is the server saying the request was wrong; repeating it verbatim
 * cannot change the answer. Only 5xx and network failures — the transient
 * ones — are worth retrying, which is the same distinction the event consumer
 * makes between a permanent error and an unavailable dependency (ADR-003).
 */
const shouldRetry = (failureCount: number, error: unknown): boolean => {
  if (failureCount >= 2) return false;
  if (error instanceof ApiError) {
    return error.isNetworkFailure || error.status >= 500;
  }
  return false;
};

export function AppProviders({ children }: { children: ReactNode }) {
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            retry: shouldRetry,
            staleTime: 5_000,
            refetchOnWindowFocus: true,
          },
          // A failed POST is never retried automatically: the contract has no
          // `Idempotency-Key`, so a retry could register the transaction twice.
          mutations: { retry: false },
        },
      }),
  );

  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
