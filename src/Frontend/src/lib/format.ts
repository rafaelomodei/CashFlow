/**
 * Presentation formatting. The single place where contract values (UTC instants,
 * `YYYY-MM-DD` civil dates, plain numbers) become pt-BR text.
 *
 * Every date function here works in UTC on purpose. The contract defines the
 * consolidation day as the UTC date of `occurredAt` (RN-004), so rendering
 * through the browser's local timezone would show a Brazilian user the balance
 * of the previous day — correct data, wrong label, no error anywhere.
 */

const currencyFormatter = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/**
 * Intl separates the symbol from the digits with a non-breaking space. It is
 * invisible on screen and it is not the character anyone types into an
 * assertion, so it is normalised away here rather than in every test.
 */
const normalizeSpaces = (value: string): string => value.replace(/ /g, ' ');

export function formatCurrency(value: number): string {
  return normalizeSpaces(currencyFormatter.format(value));
}

/** `2026-08-09` → `09/08/2026`, by string split — never through `Date`. */
export function formatCivilDate(civilDate: string): string {
  const [year, month, day] = civilDate.split('-');
  return `${day}/${month}/${year}`;
}

const pad = (value: number): string => String(value).padStart(2, '0');

/** ISO instant → `09/08`, in UTC. */
export function formatDayMonth(instant: string): string {
  const date = new Date(instant);
  return `${pad(date.getUTCDate())}/${pad(date.getUTCMonth() + 1)}`;
}

/** ISO instant → `09/08/2026 14:30`, in UTC. */
export function formatInstant(instant: string): string {
  const date = new Date(instant);
  const day = `${pad(date.getUTCDate())}/${pad(date.getUTCMonth() + 1)}/${date.getUTCFullYear()}`;
  return `${day} ${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())} UTC`;
}

const SECOND = 1000;
const MINUTE = 60 * SECOND;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/**
 * Distance from `instant` to `now`, in short pt-BR units.
 *
 * A server clock marginally ahead of the browser's would otherwise produce
 * "há -2 s", which reads as a defect. Negative distances collapse to "agora".
 */
export function formatRelativeTime(instant: string, now: Date = new Date()): string {
  const elapsed = now.getTime() - new Date(instant).getTime();

  if (elapsed < SECOND) return 'agora';
  if (elapsed < MINUTE) return `há ${Math.floor(elapsed / SECOND)} s`;
  if (elapsed < HOUR) return `há ${Math.floor(elapsed / MINUTE)} min`;
  if (elapsed < DAY) return `há ${Math.floor(elapsed / HOUR)} h`;
  return `há ${Math.floor(elapsed / DAY)} d`;
}

/**
 * The consolidation day of an instant, in UTC (RN-004).
 *
 * Used to decide which day's balance a new transaction affects. Reading the
 * local day instead would make the screen wait for a balance the worker is
 * never going to touch, and then report the consolidation as late.
 */
export function utcDayOf(instant: string): string {
  return new Date(instant).toISOString().slice(0, 10);
}

/** Today's civil date in UTC, in the `YYYY-MM-DD` shape the contract expects. */
export function todayAsCivilDate(now: Date = new Date()): string {
  return now.toISOString().slice(0, 10);
}
