import { useEffect, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getDailyBalance } from '../../api/consolidationApi';
import { SYNC_POLL_MS, SYNC_TIMEOUT_MS, syncPhase, type SyncPhase } from './syncPhase';

export const dailyBalanceKey = (date: string) => ['daily-balance', date] as const;

/**
 * The balance of a day, plus the convergence state after a registration.
 *
 * `awaitingSince` is the `createdAt` of a transaction that lands on this day.
 * The caller is responsible for only passing it when the day matches: a
 * backdated entry updates a different day, and waiting for today's balance to
 * move would poll until the deadline and then wrongly report a late worker.
 */
export function useDailyBalance(date: string, awaitingSince: string | null) {
  const [elapsedMs, setElapsedMs] = useState(0);
  const startedAt = useRef<number | null>(null);

  useEffect(() => {
    if (!awaitingSince) {
      startedAt.current = null;
      setElapsedMs(0);
      return;
    }

    startedAt.current = Date.now();
    setElapsedMs(0);

    // The deadline needs its own tick: without it, a worker that never answers
    // would leave the phase on "syncing" forever, because nothing else would
    // re-render the component once polling stopped producing changes.
    const tick = setInterval(() => {
      if (startedAt.current !== null) {
        setElapsedMs(Date.now() - startedAt.current);
      }
    }, SYNC_POLL_MS);

    return () => clearInterval(tick);
  }, [awaitingSince]);

  const query = useQuery({
    queryKey: dailyBalanceKey(date),
    queryFn: () => getDailyBalance(date),
    refetchInterval: (q) => {
      const phase = syncPhase({
        awaitingSince,
        updatedAt: q.state.data?.updatedAt,
        elapsedMs: startedAt.current === null ? 0 : Date.now() - startedAt.current,
      });
      return phase === 'syncing' ? SYNC_POLL_MS : false;
    },
  });

  const phase: SyncPhase = syncPhase({
    awaitingSince,
    updatedAt: query.data?.updatedAt,
    elapsedMs,
  });

  return { ...query, phase, timeoutSeconds: Math.round(SYNC_TIMEOUT_MS / 1000) };
}
