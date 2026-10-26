import { expect, test } from 'vitest';
import type { RolloverItemDto } from '../../../api/sprints';
import {
  defaultChoices,
  destinationOf,
  nextSprintFigures,
  pointsMovingToNext,
  rolloverMoves,
  setAll,
} from './rolloverChoices';

const unfinished = (id: string, points: number | null, next: string | null): RolloverItemDto =>
  ({ workItemId: id, points, defaultTargetSprintId: next }) as RolloverItemDto;

const items = [unfinished('a', 5, 'n'), unfinished('b', 3, 'n'), unfinished('c', null, 'n')];

test('defaultChoices sends an item to the next sprint when the server has one, else to the backlog', () => {
  const choices = defaultChoices([unfinished('a', 1, 'n'), unfinished('b', 1, null)]);
  expect([...choices]).toEqual([
    ['a', 'next'],
    ['b', 'backlog'],
  ]);
});

test('destinationOf falls back to the server default for an item nobody chose for', () => {
  expect(destinationOf(unfinished('x', 1, 'n'), new Map())).toBe('next');
  expect(destinationOf(unfinished('x', 1, null), new Map())).toBe('backlog');
});

test('pointsMovingToNext counts only the items going to the next sprint, and an unpointed item adds none', () => {
  const choices = new Map([...setAll(items, 'next')]).set('b', 'backlog');
  expect(pointsMovingToNext(items, choices)).toBe(5);
  expect(pointsMovingToNext(items, setAll(items, 'stay'))).toBe(0);
  expect(pointsMovingToNext(items, setAll(items, 'next'))).toBe(8);
});

test('nextSprintFigures is over by the excess, and not over at exactly the capacity', () => {
  expect(nextSprintFigures(10, 20, 11)).toEqual({ total: 21, capacity: 20, over: 1, room: 0 });
  expect(nextSprintFigures(10, 20, 10)).toEqual({ total: 20, capacity: 20, over: 0, room: 0 });
  expect(nextSprintFigures(10, 20, 4)).toEqual({ total: 14, capacity: 20, over: 0, room: 6 });
});

test('nextSprintFigures never warns when no capacity is entered', () => {
  expect(nextSprintFigures(10, null, 50)).toEqual({ total: 60, capacity: null, over: 0, room: 0 });
});

test('rolloverMoves sends a story to the next sprint or the backlog and leaves a stayed one out', () => {
  const choices = new Map([
    ['a', 'next'],
    ['b', 'backlog'],
    ['c', 'stay'],
  ] as const);
  expect(rolloverMoves(items, choices, 'n')).toEqual([
    { workItemId: 'a', targetSprintId: 'n' },
    { workItemId: 'b', targetSprintId: null },
  ]);
});
