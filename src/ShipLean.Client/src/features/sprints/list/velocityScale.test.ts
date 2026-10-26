import { expect, test } from 'vitest';
import { velocityScale } from './velocityScale';

test('velocityScale_WhenNoValues_ShouldBeTen', () => {
  expect(velocityScale([])).toEqual({ max: 10, ticks: [0, 5, 10] });
});

test('velocityScale_WhenLargestIsPastATen_ShouldRoundUp', () => {
  expect(velocityScale([4, 21, 18])).toEqual({ max: 30, ticks: [0, 15, 30] });
  expect(velocityScale([20])).toEqual({ max: 20, ticks: [0, 10, 20] });
});
