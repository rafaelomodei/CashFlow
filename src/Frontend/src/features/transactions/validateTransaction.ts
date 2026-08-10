import type { TransactionType } from '../../api/types';

/**
 * Client-side checks on the transaction form.
 *
 * These are **not** the business rules — those live in the domain, and the API
 * remains the authority. What is mirrored here are the documented limits of
 * `api-contracts.md` §1.4 and §2.1, so the user gets the answer in the field
 * instead of a round trip. Anything this misses is still caught by the server,
 * and the resulting 400 is rendered field by field (§4.2).
 *
 * The rule to keep: this file may never become more permissive *or* more
 * restrictive than the contract in a way that changes what the system accepts.
 */

export interface TransactionDraft {
  type: TransactionType;
  amount: string;
  occurredOn: string;
  description: string;
}

export type FieldErrors = Record<string, string[]>;

/** Upper bound of `numeric(18,2)` — ADR-005 and contract §1.4. */
const MAX_AMOUNT = 9_999_999_999_999_999.99;
const MAX_DESCRIPTION = 200;

/**
 * Masked field text → the plain notation `Number` reads: `1.500,50` → `1500.50`.
 *
 * The dot is the pt-BR thousands separator here and never a decimal point. That
 * is safe because `maskAmount` is what produces the string: it is the only shape
 * the amount field can hold.
 */
const normalizeAmount = (raw: string): string => raw.trim().replace(/\./g, '').replace(',', '.');

/** `1.500,50` → `1500.5`. */
export function parseAmount(raw: string): number {
  return Number(normalizeAmount(raw));
}

const decimalPlaces = (raw: string): number => {
  const normalized = normalizeAmount(raw);
  const dot = normalized.indexOf('.');
  return dot === -1 ? 0 : normalized.length - dot - 1;
};

export function validateTransactionDraft(draft: TransactionDraft): FieldErrors {
  const errors: FieldErrors = {};

  const rawAmount = draft.amount.trim();
  if (!rawAmount) {
    errors.amount = ['Informe o valor.'];
  } else {
    const amount = parseAmount(rawAmount);
    if (!Number.isFinite(amount)) {
      errors.amount = ['Valor inválido.'];
    } else if (amount <= 0) {
      errors.amount = ['O valor deve ser maior que zero.'];
    } else if (decimalPlaces(rawAmount) > 2) {
      errors.amount = ['O valor deve ter no máximo duas casas decimais.'];
    } else if (amount > MAX_AMOUNT) {
      errors.amount = ['Valor acima do limite aceito.'];
    }
  }

  if (draft.description.length > MAX_DESCRIPTION) {
    errors.description = [`A descrição deve ter no máximo ${MAX_DESCRIPTION} caracteres.`];
  }

  if (draft.occurredOn && Number.isNaN(Date.parse(`${draft.occurredOn}T00:00:00Z`))) {
    errors.occurredAt = ['Data inválida.'];
  }

  return errors;
}
