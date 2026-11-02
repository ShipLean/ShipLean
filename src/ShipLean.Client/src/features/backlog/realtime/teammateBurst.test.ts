import { expect, test } from 'vitest';
import { burstText, isBurst } from './teammateBurst';

test('isBurst_WhenThreeEventsInTheWindow_ShouldBeFalse', () => {
  expect(isBurst([0, 100, 200], 200)).toBe(false);
});

test('isBurst_WhenFourEventsInTheWindow_ShouldBeTrue', () => {
  expect(isBurst([0, 100, 200, 300], 300)).toBe(true);
});

test('isBurst_WhenFourEventsButSomeOutsideTheWindow_ShouldBeFalse', () => {
  expect(isBurst([0, 100, 700, 800], 800)).toBe(false);
});

test('burstText_WhenManyOrOne_ShouldCountItems', () => {
  expect(burstText(12)).toBe('12 items changed by a teammate');
  expect(burstText(1)).toBe('1 item changed by a teammate');
});
