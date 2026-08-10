/**
 * The mask of the amount field.
 *
 * Money is typed as digits and nothing else: every other character is dropped
 * before it reaches the state, so a letter or a stray separator never becomes a
 * value the contract would have to reject (§1.4).
 *
 * The digits are read right to left as centavos — `150050` becomes `1.500,50`.
 * That reading is what makes the mask predictable: typing, pasting, deleting and
 * replacing a selection all end at the same place, so no cursor bookkeeping is
 * needed on a controlled input that always re-renders with the caret at the end.
 *
 * This does not replace `validateTransactionDraft`. The mask decides what can be
 * typed; the validation keeps mirroring the contract for everything else.
 */

/** `numeric(18,2)` — ADR-005: sixteen integer digits plus the two centavos. */
const MAX_DIGITS = 18;

/** `1500` → `1.500`. The dot is the pt-BR thousands separator, never a decimal point. */
const groupThousands = (integer: string): string => integer.replace(/\B(?=(\d{3})+(?!\d))/g, '.');

export function maskAmount(raw: string): string {
  const digits = raw
    .replace(/\D/g, '')
    // A leading zero is what the previous keystroke left behind (`0,07` is stored
    // as `007`); keeping it would let the amount grow past the contract limit.
    .replace(/^0+(?=\d)/, '')
    .slice(0, MAX_DIGITS);

  if (digits === '') return '';

  const centavos = digits.padStart(3, '0');
  return `${groupThousands(centavos.slice(0, -2))},${centavos.slice(-2)}`;
}
