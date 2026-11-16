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

const EVERY_DAY = [1, 2, 3, 4, 5, 6, 7];

// Mutation: an off-by-one in the days-from-civil month table (e.g. the March-based offset) shifts these dates.
test('sprintWorkingDays_WhenSprintCrossesAYearAndALeapDay_ShouldListEveryCivilDate', () => {
  expect(sprintWorkingDays('2023-12-30', '2024-01-02', EVERY_DAY)).toEqual([
    '2023-12-30',
    '2023-12-31',
    '2024-01-01',
    '2024-01-02',
  ]);
  expect(sprintWorkingDays('2024-02-28', '2024-03-01', EVERY_DAY)).toEqual([
    '2024-02-28',
    '2024-02-29',
    '2024-03-01',
  ]);
  expect(sprintWorkingDays('2100-02-28', '2100-03-01', EVERY_DAY)).toEqual([
    '2100-02-28',
    '2100-03-01',
  ]);
  expect(sprintWorkingDays('2026-12-31', '2027-01-01', [4, 5])).toEqual([
    '2026-12-31',
    '2027-01-01',
  ]);
});
