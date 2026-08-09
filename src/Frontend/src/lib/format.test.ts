import { describe, expect, it } from 'vitest';
import {
  formatCurrency,
  formatDayMonth,
  formatCivilDate,
  formatRelativeTime,
  todayAsCivilDate,
  utcDayOf,
} from './format';

describe('formatCurrency', () => {
  it('formats a value in Brazilian currency', () => {
    expect(formatCurrency(1500)).toBe('R$ 1.500,00');
  });

  it('always shows two decimal places', () => {
    expect(formatCurrency(500.5)).toBe('R$ 500,50');
    expect(formatCurrency(0)).toBe('R$ 0,00');
  });

  it('formats a negative balance', () => {
    expect(formatCurrency(-800)).toBe('-R$ 800,00');
  });

  it('groups thousands', () => {
    expect(formatCurrency(1234567.89)).toBe('R$ 1.234.567,89');
  });
});

describe('formatCivilDate', () => {
  it('formats a civil date without shifting it to the local timezone', () => {
    // The contract defines `2026-08-09` as a civil date in UTC. Parsing it as a
    // local instant would render it as 08/08 anywhere west of Greenwich — the
    // balance of the wrong day, shown convincingly.
    expect(formatCivilDate('2026-08-09')).toBe('09/08/2026');
  });

  it('keeps the first day of a month intact', () => {
    expect(formatCivilDate('2026-01-01')).toBe('01/01/2026');
  });
});

describe('formatDayMonth', () => {
  it('formats an instant as day/month in UTC', () => {
    expect(formatDayMonth('2026-08-09T14:30:00Z')).toBe('09/08');
  });

  it('does not roll an early-morning UTC instant back a day', () => {
    // 00:30Z is 21:30 of the previous day in Brasília. The consolidation day is
    // the UTC one (RN-004), so the table has to agree with the balance.
    expect(formatDayMonth('2026-08-09T00:30:00Z')).toBe('09/08');
  });
});

describe('formatRelativeTime', () => {
  const now = new Date('2026-08-09T12:00:00Z');

  it('reports sub-minute distances in seconds', () => {
    expect(formatRelativeTime('2026-08-09T11:59:57Z', now)).toBe('há 3 s');
  });

  it('reports minutes', () => {
    expect(formatRelativeTime('2026-08-09T11:58:00Z', now)).toBe('há 2 min');
  });

  it('reports hours', () => {
    expect(formatRelativeTime('2026-08-09T09:00:00Z', now)).toBe('há 3 h');
  });

  it('treats an instant in the immediate past as "agora"', () => {
    expect(formatRelativeTime('2026-08-09T12:00:00Z', now)).toBe('agora');
  });

  it('never reports a negative distance for clock skew', () => {
    // The server clock can be marginally ahead of the browser's. "há -2 s" is
    // the kind of detail that makes a correct system look broken.
    expect(formatRelativeTime('2026-08-09T12:00:02Z', now)).toBe('agora');
  });
});

describe('utcDayOf', () => {
  it('extracts the consolidation day of an instant', () => {
    expect(utcDayOf('2026-08-09T14:30:00Z')).toBe('2026-08-09');
  });

  it('uses the UTC day even when the instant carries another offset', () => {
    // 22:00 in Brasília is already the next day in UTC, and the consolidation
    // follows UTC (RN-004). Deciding which day to wait for by the local reading
    // would watch a day the worker is never going to touch.
    expect(utcDayOf('2026-08-09T22:00:00-03:00')).toBe('2026-08-10');
  });
});

describe('todayAsCivilDate', () => {
  it('returns the UTC civil date, not the local one', () => {
    const lateAtNightInBrasilia = new Date('2026-08-10T01:00:00Z');
    expect(todayAsCivilDate(lateAtNightInBrasilia)).toBe('2026-08-10');
  });
});
