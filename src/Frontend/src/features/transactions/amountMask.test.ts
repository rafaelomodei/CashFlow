import { describe, expect, it } from 'vitest';
import { maskAmount } from './amountMask';

describe('maskAmount', () => {
  it('reads the digits as centavos, right to left', () => {
    expect(maskAmount('1')).toBe('0,01');
    expect(maskAmount('15')).toBe('0,15');
    expect(maskAmount('150')).toBe('1,50');
    expect(maskAmount('150050')).toBe('1.500,50');
  });

  it('groups the thousands', () => {
    expect(maskAmount('123456789')).toBe('1.234.567,89');
  });

  it('drops everything that is not a digit', () => {
    // The point of the mask: what the contract rejects never reaches the state,
    // so the user is not told afterwards that the letters were not accepted.
    expect(maskAmount('abc')).toBe('');
    expect(maskAmount('1a5b0c0d5e0')).toBe('1.500,50');
    expect(maskAmount('R$ 1.500,50')).toBe('1.500,50');
    expect(maskAmount('-10')).toBe('0,10');
  });

  it('keeps a typed separator from adding a third decimal place', () => {
    expect(maskAmount('1500,005')).toBe('15.000,05');
  });

  it('empties the field when the last digit is deleted', () => {
    expect(maskAmount('')).toBe('');
    expect(maskAmount(',')).toBe('');
  });

  it('deletes right to left, the way the digits were entered', () => {
    // Backspace on `1.500,50` hands back `1.500,5`, which has to read as `150,05`.
    expect(maskAmount('1.500,5')).toBe('150,05');
  });

  it('does not let leading zeros push the value past the contract limit', () => {
    expect(maskAmount('000123')).toBe('1,23');
    // Sixteen integer digits and two centavos is the whole of `numeric(18,2)`.
    expect(maskAmount('9'.repeat(20))).toBe('9.999.999.999.999.999,99');
  });
});
