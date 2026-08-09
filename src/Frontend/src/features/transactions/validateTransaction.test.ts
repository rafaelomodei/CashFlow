import { describe, expect, it } from 'vitest';
import { validateTransactionDraft } from './validateTransaction';

const valid = { type: 'CREDIT' as const, amount: '1500,00', occurredOn: '2026-08-09', description: '' };

describe('validateTransactionDraft', () => {
  it('accepts a valid draft', () => {
    expect(validateTransactionDraft(valid)).toEqual({});
  });

  it('requires an amount', () => {
    expect(validateTransactionDraft({ ...valid, amount: '' }).amount).toBeDefined();
  });

  it('rejects zero and negative amounts (RN-001)', () => {
    expect(validateTransactionDraft({ ...valid, amount: '0' }).amount).toBeDefined();
    expect(validateTransactionDraft({ ...valid, amount: '-10' }).amount).toBeDefined();
  });

  it('rejects more than two decimal places instead of rounding', () => {
    // §1.4 of the contract: 1500.005 is a 400, never a silent round. Rounding
    // here would produce a value the user never typed.
    expect(validateTransactionDraft({ ...valid, amount: '1500,005' }).amount).toBeDefined();
  });

  it('accepts exactly two decimal places', () => {
    expect(validateTransactionDraft({ ...valid, amount: '0,01' })).toEqual({});
  });

  it('rejects an amount that is not a number', () => {
    expect(validateTransactionDraft({ ...valid, amount: 'abc' }).amount).toBeDefined();
  });

  it('rejects an amount beyond the numeric(18,2) range', () => {
    expect(
      validateTransactionDraft({ ...valid, amount: '99999999999999999' }).amount,
    ).toBeDefined();
  });

  it('rejects a description longer than 200 characters', () => {
    expect(
      validateTransactionDraft({ ...valid, description: 'a'.repeat(201) }).description,
    ).toBeDefined();
  });

  it('accepts a description of exactly 200 characters', () => {
    expect(validateTransactionDraft({ ...valid, description: 'a'.repeat(200) })).toEqual({});
  });

  it('accepts an absent date, letting the server stamp it', () => {
    expect(validateTransactionDraft({ ...valid, occurredOn: '' })).toEqual({});
  });

  it('reports every invalid field at once, never one at a time', () => {
    // §4.2: the API reports all of them together, and a client that stopped at
    // the first would put the user through a guess-and-retry cycle.
    const errors = validateTransactionDraft({
      type: 'CREDIT',
      amount: '-1',
      occurredOn: '',
      description: 'a'.repeat(201),
    });
    expect(Object.keys(errors).sort()).toEqual(['amount', 'description']);
  });
});

describe('parseAmount', () => {
  it('reads the Brazilian decimal comma as a number', async () => {
    const { parseAmount } = await import('./validateTransaction');
    expect(parseAmount('1500,50')).toBe(1500.5);
  });

  it('reads a plain dot decimal too', async () => {
    const { parseAmount } = await import('./validateTransaction');
    expect(parseAmount('1500.50')).toBe(1500.5);
  });
});
