import { expect, test } from 'vitest';
import type { VelocityDto } from '../../../api/sprints';
import type { BacklogSectionDto } from '../../../api/views';
import { itemRow } from '../ct/fixtures';
import { sprintSum } from './sprintSum';

const sprint = { id: 'sp25', name: 'Sprint 25', status: 'future' } as const;
const section = (over: Partial<BacklogSectionDto> = {}): BacklogSectionDto => ({
  id: 'sp25',
  title: 'Sprint 25',
  sprint,
  points: 13,
  capacity: 22,
  rows: [],
  ...over,
});
const velocity = (average: number, override: number | null = null): VelocityDto => ({
  window: 3,
  sprints: [],
  average,
  override,
});
const rows = [itemRow({ key: 'SHIP-1', points: 5 }), itemRow({ key: 'SHIP-2', points: 3 })];

test('13 + 8 = 21 / 22', () => {
  const sum = sprintSum(section(), rows, null);

  expect(sum.text).toBe('13 + 8 = 21 / 22');
  expect([sum.now, sum.adding, sum.after, sum.of, sum.over]).toEqual([13, 8, 21, 22, false]);
});

test('a sprint with no capacity uses velocity', () => {
  expect(sprintSum(section({ capacity: null }), rows, velocity(20)).text).toBe('13 + 8 = 21 / 20');
  expect(sprintSum(section({ capacity: null }), rows, velocity(20, 18)).of).toBe(18);
});

test('rows already in the sprint add nothing', () => {
  const sum = sprintSum(section(), [...rows, itemRow({ key: 'SHIP-3', points: 8, sprint })], null);

  expect(sum.adding).toBe(8);
});

test('unestimated count zero', () => {
  const sum = sprintSum(section(), [itemRow({ key: 'SHIP-5', points: null })], null);

  expect(sum.text).toBe('13 + 0 = 13 / 22');
});

test('over is true past the limit', () => {
  expect(sprintSum(section({ capacity: 20 }), rows, null).over).toBe(true);
  expect(sprintSum(section({ capacity: 21 }), rows, null).over).toBe(false);
});

test('no sprint limit at all omits / n', () => {
  const sum = sprintSum(section({ capacity: null }), rows, null);

  expect(sum.text).toBe('13 + 8 = 21');
  expect(sum.of).toBeNull();
  expect(sum.over).toBe(false);
});

test('the Backlog section counts rows with no sprint as already there', () => {
  const backlog = section({ id: 'backlog', sprint: null, points: 16, capacity: null });

  expect(sprintSum(backlog, [itemRow({ key: 'SHIP-6', points: 5 })], null).adding).toBe(0);
});
