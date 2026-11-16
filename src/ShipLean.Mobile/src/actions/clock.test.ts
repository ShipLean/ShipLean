import { expect, test } from 'vitest';
import { isoAt } from './clock';

// Mutation: isoAt returning local time without the Z (e.g. a toString-based format) turns this red.
test('isoAt_WhenGivenEpochMilliseconds_ShouldReturnUtcIsoWithZ', () => {
  expect(isoAt(Date.UTC(2026, 10, 16, 0, 2, 5, 7))).toBe('2026-11-16T00:02:05.007Z');
  expect(isoAt(0)).toBe('1970-01-01T00:00:00.000Z');
});
