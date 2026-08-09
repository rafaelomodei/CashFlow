import { describe, expect, it } from 'vitest';
import { SYNC_TIMEOUT_MS, syncPhase } from './syncPhase';

const since = '2026-08-09T12:00:00Z';

describe('syncPhase', () => {
  it('is idle when nothing is being awaited', () => {
    expect(syncPhase({ awaitingSince: null, updatedAt: '2026-08-09T11:00:00Z', elapsedMs: 0 })).toBe(
      'idle',
    );
  });

  it('is syncing while the balance is older than the registration', () => {
    expect(
      syncPhase({ awaitingSince: since, updatedAt: '2026-08-09T11:59:00Z', elapsedMs: 2_000 }),
    ).toBe('syncing');
  });

  it('is syncing when the day has never been consolidated', () => {
    // First entry of the day: `updatedAt` is null until the worker applies it.
    expect(syncPhase({ awaitingSince: since, updatedAt: null, elapsedMs: 500 })).toBe('syncing');
  });

  it('returns to idle once updatedAt reaches the registration instant', () => {
    expect(
      syncPhase({ awaitingSince: since, updatedAt: '2026-08-09T12:00:00Z', elapsedMs: 3_000 }),
    ).toBe('idle');
  });

  it('returns to idle once updatedAt passes the registration instant', () => {
    expect(
      syncPhase({ awaitingSince: since, updatedAt: '2026-08-09T12:00:04Z', elapsedMs: 4_000 }),
    ).toBe('idle');
  });

  it('gives up and reports a stale consolidation after the deadline', () => {
    // Without a deadline this is an infinite poll: a forgotten tab would query
    // the balance forever while the worker is down.
    expect(
      syncPhase({ awaitingSince: since, updatedAt: null, elapsedMs: SYNC_TIMEOUT_MS + 1 }),
    ).toBe('stale');
  });

  it('prefers convergence over the deadline when both are true', () => {
    // A slow last poll that arrives converged must not be reported as stale.
    expect(
      syncPhase({
        awaitingSince: since,
        updatedAt: '2026-08-09T12:00:01Z',
        elapsedMs: SYNC_TIMEOUT_MS + 5_000,
      }),
    ).toBe('idle');
  });
});
