import { expect, test } from 'vitest';
import { IMPACT_WIDTH, impactShape } from './impactGeometry';

test('impactShape_WhenCountsGiven_ShouldMakeOneBarPerSprintInOrder', () => {
  const shape = impactShape([3, 4, 2, 0, 1, 0], 2, 3, 0.5);
  expect(shape.bars).toHaveLength(6);
  expect(shape.bars.map((b) => b.count)).toEqual([3, 4, 2, 0, 1, 0]);
  expect(shape.bars.map((b) => b.after)).toEqual([false, false, false, true, true, true]);
  expect(shape.bars[0].x).toBeLessThan(shape.bars[1].x);
});

test('impactShape_WhenAveragesEqualABarsCount_ShouldPutTheLineAtThatBarsTop', () => {
  const shape = impactShape([3, 4, 2, 0, 1, 0], 2, 3, 1);
  expect(shape.before?.y).toBe(shape.bars[0].y);
  expect(shape.after?.y).toBe(shape.bars[4].y);
  expect(shape.before?.value).toBe(3);
  expect(shape.after?.value).toBe(1);
});

test('impactShape_WhenAverageIsFractional_ShouldScaleLinearlyBetweenZeroAndTheTop', () => {
  const shape = impactShape([3, 4, 2, 0, 1, 0], 2, 3, 0.5);
  const zero = shape.ticks[0].y;
  const top = shape.ticks[2].y;
  expect(shape.max).toBe(4);
  expect(shape.after?.y).toBeCloseTo(zero - (zero - top) * (0.5 / 4), 2);
  expect(shape.before?.y).toBeCloseTo(zero - (zero - top) * (3 / 4), 2);
});

test('impactShape_WhenAfterAverageIsNull_ShouldDrawNoAfterLine', () => {
  const shape = impactShape([1, 2, 2, 3, 2, 3], 4, 2, null);
  expect(shape.after).toBeNull();
  expect(shape.before?.y).toBeDefined();
});

test('impactShape_WhenLinesDrawn_ShouldSplitAtTheMarker', () => {
  const shape = impactShape([3, 4, 2, 0, 1, 0], 2, 3, 0.5);
  expect(shape.before?.x2).toBe(shape.markerX);
  expect(shape.after?.x1).toBe(shape.markerX);
  expect(shape.after?.x2).toBe(IMPACT_WIDTH);
  expect(shape.markerX).toBeGreaterThan(shape.before?.x1 ?? Infinity);
});

test('impactShape_WhenClosedBeforeTheRange_ShouldPutTheMarkerOnTheLeftEdgeAndDrawNoBeforeLine', () => {
  const shape = impactShape([1, 1, 2], -1, 0, 1.5);
  expect(shape.markerX).toBeLessThan(shape.bars[0].x);
  expect(shape.before).toBeNull();
  expect(shape.after?.x1).toBe(shape.markerX);
  expect(shape.bars.every((b) => b.after)).toBe(true);
});

test('impactShape_WhenCountsExceedTheDefaultTop_ShouldRaiseTheScale', () => {
  expect(impactShape([1, 2], 0, 1, 1).max).toBe(4);
  expect(impactShape([7, 2], 0, 1, 1).max).toBe(8);
});
