import { expect, test } from 'vitest';
import { withRecent } from './shellApi';

test('withRecent_WhenProjectAlreadyListed_ShouldMoveItToTheFront', () => {
  expect(withRecent(['a', 'b', 'c'], 'c')).toEqual(['c', 'a', 'b']);
});

test('withRecent_WhenMoreThanFive_ShouldDropTheOldest', () => {
  expect(withRecent(['a', 'b', 'c', 'd', 'e'], 'f')).toEqual(['f', 'a', 'b', 'c', 'd']);
});
