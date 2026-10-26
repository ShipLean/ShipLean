import { expect, test } from 'vitest';
import { sprintWorkingDays } from './sprintWorkingDays';

const WEEK = [1, 2, 3, 4, 5];

test('sprintWorkingDays lists ten days for a two week sprint and skips the weekend between', () => {
  const days = sprintWorkingDays('2026-10-19', '2026-10-30', WEEK);
  expect(days).toHaveLength(10);
  expect(days[4]).toBe('2026-10-23');
  expect(days[5]).toBe('2026-10-26');
});

test('sprintWorkingDays honours a project that works Sunday and not Wednesday', () => {
  expect(sprintWorkingDays('2026-10-18', '2026-10-21', [7, 1, 2])).toEqual([
    '2026-10-18',
    '2026-10-19',
    '2026-10-20',
  ]);
});

test('sprintWorkingDays crosses a month end', () => {
  expect(sprintWorkingDays('2026-10-30', '2026-11-02', WEEK)).toEqual(['2026-10-30', '2026-11-02']);
});

test('sprintWorkingDays returns nothing when the sprint falls on days off', () => {
  expect(sprintWorkingDays('2026-10-24', '2026-10-25', WEEK)).toEqual([]);
});
