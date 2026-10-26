import { describe, expect, test } from 'vitest';
import { burndownShape } from './burndownGeometry';
import { sprintWorkingDays } from './sprintWorkingDays';

const days = sprintWorkingDays('2026-10-19', '2026-10-30', [1, 2, 3, 4, 5]);
const point = (date: string, remaining: number) => ({ date, remaining, ideal: 0 });
const right = 226;
const left = 24;

describe('burndownShape', () => {
  test('on day 1 of 10 draws one dot at the left and an ideal line across the whole sprint', () => {
    const shape = burndownShape([point('2026-10-19', 21)], days, 21);
    expect(shape.dots).toHaveLength(1);
    expect(shape.dots[0]).toMatchObject({ x: left, y: 6, day: 19 });
    const ideal = shape.idealPoints.split(' ');
    expect(ideal).toHaveLength(10);
    expect(ideal[0]).toBe('24,6');
    expect(ideal[9]).toBe(`${right},${shape.baselineY}`);
  });

  test('mid-sprint puts the last of four points at the fourth day, not at the right edge', () => {
    const shape = burndownShape(
      ['2026-10-19', '2026-10-20', '2026-10-21', '2026-10-22'].map((d) => point(d, 16)),
      days,
      21,
    );
    expect(shape.dots[3]!.x).toBeCloseTo(left + (3 * (right - left)) / 9, 1);
    expect(shape.dots[3]!.x).toBeLessThan(right);
  });

  test('on the last day the final dot sits at the right edge', () => {
    const shape = burndownShape(
      days.map((d) => point(d, 0)),
      days,
      21,
    );
    expect(shape.dots).toHaveLength(10);
    expect(shape.dots[9]).toMatchObject({ x: right, y: shape.baselineY });
  });

  test('a weekend between two points leaves no gap in the axis and no dot for it', () => {
    const shape = burndownShape([point('2026-10-23', 10), point('2026-10-26', 8)], days, 21);
    const step = (right - left) / 9;
    expect(shape.dots[1]!.x - shape.dots[0]!.x).toBeCloseTo(step, 1);
    expect(shape.dots.map((d) => d.day)).toEqual([23, 26]);
  });

  test('a lower remaining is drawn lower than a higher one', () => {
    const [first, second] = burndownShape(
      [point('2026-10-19', 21), point('2026-10-20', 10)],
      days,
      21,
    ).dots;
    expect(second!.y).toBeGreaterThan(first!.y);
  });

  test('scales to the committed points when nothing has burned yet', () => {
    expect(burndownShape([], days, 8).max).toBe(8);
  });

  test('a sprint with nothing committed keeps its dot on the baseline', () => {
    const shape = burndownShape([point('2026-10-19', 0)], days, 0);
    expect(shape.dots[0]!.y).toBe(shape.baselineY);
  });

  test('a one day sprint still has an ideal line point', () => {
    const shape = burndownShape([], ['2026-10-19'], 5);
    expect(shape.idealPoints).toBe(`${left},${shape.baselineY}`);
  });
});
