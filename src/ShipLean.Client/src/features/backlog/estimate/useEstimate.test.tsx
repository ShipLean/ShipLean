import { describe, expect, it } from 'vitest';
import { parsePoints } from './useEstimate';

describe('parsePoints', () => {
  it('ParsePoints_WhenEmpty_ShouldClear', () => {
    expect(parsePoints('  ')).toEqual({ ok: true, value: null });
  });

  it.each([
    ['5', 5],
    ['0', 0],
    ['2.5', 2.5],
    ['9999.99', 9999.99],
  ])('ParsePoints_WhenValid_ShouldSet %s', (text, value) => {
    expect(parsePoints(text)).toEqual({ ok: true, value });
  });

  it.each(['abc', '-1', '1.234', '10000', '1e3', '5 3'])(
    'ParsePoints_WhenInvalid_ShouldRefuse %s',
    (text) => {
      expect(parsePoints(text)).toEqual({ ok: false });
    },
  );
});
