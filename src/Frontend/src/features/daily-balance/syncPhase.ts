/**
 * The eventual-consistency state machine, as a pure function.
 *
 * After a `201`, the balance of that day is stale by definition — the worker
 * has not applied the event yet (ADR-006). The screen polls until `updatedAt`
 * catches up with the registration instant, and then stops.
 *
 * The deadline is the part that matters. A `refetchInterval` that only ends on
 * success polls forever whenever the worker is down, and a forgotten tab would
 * keep asking. Reaching the deadline is not an error: it is the correct
 * diagnosis — the consolidation is behind — and the card says so.
 */

export const SYNC_POLL_MS = 1_500;
export const SYNC_TIMEOUT_MS = 20_000;

export type SyncPhase = 'idle' | 'syncing' | 'stale';

interface SyncPhaseInput {
  /** Instant of the registration being awaited, or null when nothing is. */
  awaitingSince: string | null;
  /** `updatedAt` of the balance currently on screen. */
  updatedAt: string | null | undefined;
  /** Time since the wait started. */
  elapsedMs: number;
}

export function syncPhase({ awaitingSince, updatedAt, elapsedMs }: SyncPhaseInput): SyncPhase {
  if (!awaitingSince) return 'idle';

  // Convergence is checked before the deadline: a slow poll that comes back
  // already converged must not be reported as stale.
  const converged =
    updatedAt != null && new Date(updatedAt).getTime() >= new Date(awaitingSince).getTime();
  if (converged) return 'idle';

  return elapsedMs > SYNC_TIMEOUT_MS ? 'stale' : 'syncing';
}
