import { expect, test } from 'vitest';
import { viewportOf } from './viewport';

test('viewportOf_WhenUnder600_ShouldBePhone', () => {
  expect(viewportOf(599)).toBe('phone');
});

test('viewportOf_WhenFrom600To900_ShouldBeTablet', () => {
  expect(viewportOf(600)).toBe('tablet');
  expect(viewportOf(900)).toBe('tablet');
});

test('viewportOf_WhenOver900_ShouldBeDesktop', () => {
  expect(viewportOf(901)).toBe('desktop');
});
