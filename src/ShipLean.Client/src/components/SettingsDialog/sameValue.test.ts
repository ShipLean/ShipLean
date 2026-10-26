import { describe, expect, it } from 'vitest';
import { sameValue } from './sameValue';

describe('sameValue', () => {
  it('sameValue_WhenKeyOrderDiffers_ShouldBeEqual', () => {
    expect(sameValue({ a: 1, b: [1, { c: null }] }, { b: [1, { c: null }], a: 1 })).toBe(true);
  });

  it('sameValue_WhenNestedValueDiffers_ShouldNotBeEqual', () => {
    expect(sameValue({ a: [1, 2] }, { a: [1, 3] })).toBe(false);
    expect(sameValue({ a: null }, { a: {} })).toBe(false);
    expect(sameValue([], {})).toBe(false);
  });
});
