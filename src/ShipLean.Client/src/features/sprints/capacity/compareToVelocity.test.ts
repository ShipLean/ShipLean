import { expect, test } from 'vitest';
import { compareToVelocity } from './compareToVelocity';

test('compareToVelocity_WhenNothingEntered_ShouldNotCompare', () => {
  expect(compareToVelocity(0, false, 21)).toEqual({ kind: 'none' });
});

test('compareToVelocity_WhenNoVelocityYet_ShouldNotCompare', () => {
  expect(compareToVelocity(12, true, 0)).toEqual({ kind: 'none' });
});

test('compareToVelocity_WhenExactlyTenPercentAway_ShouldBeInLine', () => {
  expect(compareToVelocity(22, true, 20)).toEqual({ kind: 'inLine' });
  expect(compareToVelocity(18, true, 20)).toEqual({ kind: 'inLine' });
});

test('compareToVelocity_WhenMoreThanTenPercentAbove_ShouldWarnAbove', () => {
  expect(compareToVelocity(23, true, 20)).toEqual({ kind: 'above', percent: 15 });
});

test('compareToVelocity_WhenMoreThanTenPercentBelow_ShouldWarnBelow', () => {
  expect(compareToVelocity(15, true, 20)).toEqual({ kind: 'below', percent: 25 });
});

test('compareToVelocity_WhenEnteredZero_ShouldWarnBelow', () => {
  expect(compareToVelocity(0, true, 20)).toEqual({ kind: 'below', percent: 100 });
});
