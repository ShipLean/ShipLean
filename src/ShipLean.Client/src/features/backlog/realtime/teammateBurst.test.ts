import { expect, test } from 'vitest';
import { burstText, isBurst } from './teammateBurst';

test('isBurst_WhenThreeRows_ShouldBeFalse', () => {
  expect(isBurst(3)).toBe(false);
});

test('isBurst_WhenFourRows_ShouldBeTrue', () => {
  expect(isBurst(4)).toBe(true);
});

test('burstText_WhenManyOrOne_ShouldCountItems', () => {
  expect(burstText(12)).toBe('12 items changed by a teammate');
  expect(burstText(1)).toBe('1 item changed by a teammate');
});
