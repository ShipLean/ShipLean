import { describe, expect, test } from 'vitest';
import { burndownShape } from './burndownGeometry';
import { burndownDotAt, burndownReadout } from './burndownReadout';
import { sprintWorkingDays } from './sprintWorkingDays';

const days = sprintWorkingDays('2026-10-19', '2026-10-30', [1, 2, 3, 4, 5]);
const points = [
  { date: '2026-10-19', remaining: 21, ideal: 21 },
  { date: '2026-10-20', remaining: 21, ideal: 18.7 },
  { date: '2026-10-21', remaining: 19, ideal: 16.3 },
];
// Width 366 spaces the nine gaps 37.3 apart from x=24: the first three days sit at 24, 61.3 and 98.7.
const shape = burndownShape(points, days, 21, { width: 366, height: 118 });

describe('burndownDotAt', () => {
  test('picks the day whose point is closest to x', () => {
    expect(burndownDotAt(shape.dots, 66)?.date).toBe('2026-10-20');
    expect(burndownDotAt(shape.dots, 90)?.date).toBe('2026-10-21');
  });

  test('keeps a drag past either end on the end day', () => {
    expect(burndownDotAt(shape.dots, -40)?.date).toBe('2026-10-19');
    expect(burndownDotAt(shape.dots, 360)?.date).toBe('2026-10-21');
  });

  test('has no day to pick before the sprint has a point', () => {
    expect(burndownDotAt([], 50)).toBeUndefined();
  });
});

describe('burndownReadout', () => {
  test('reads the date, the points left and the ideal for the day', () => {
    expect(burndownReadout(shape.dots[2]!)).toBe('21 Oct · 19 left · ideal 16.3');
  });

  test('names the month of a day in a sprint that spans two months', () => {
    const dot = { ...shape.dots[0]!, date: '2026-11-02', day: 2, remaining: 8, ideal: 9 };
    expect(burndownReadout(dot)).toBe('2 Nov · 8 left · ideal 9');
  });
});
