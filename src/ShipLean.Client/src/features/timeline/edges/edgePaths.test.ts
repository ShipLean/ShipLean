import { describe, expect, it } from 'vitest';
import type { TimelineEdgeDto } from '@shiplean/core/api/views';
import type { Mark, MarkKind } from '../chart/timelineGeometry';
import { edgePaths } from './edgePaths';

const mark = (key: string, x0: number, x1: number, y: number, kind: MarkKind = 'bar'): Mark => ({
  key,
  kind,
  row: 0,
  box: { key, x0, x1, y, height: 10 },
  reviews: [],
  reviewLine: null,
  durationLabel: null,
});
const boxes = [mark('A', 10, 50, 0), mark('B', 80, 120, 20), mark('C', 30, 60, 40)];
const scale = { pxPerHour: 2, originX: 0 };
const edge = (over: Partial<TimelineEdgeDto>): TimelineEdgeDto => ({
  from: 'A',
  to: 'B',
  kind: 'depends',
  source: 'link',
  waitHours: null,
  waitLabel: null,
  unblockedAt: null,
  unblockedHours: null,
  onCriticalPath: false,
  ...over,
});

describe('edgePaths', () => {
  it('draws a depends elbow from the from box right edge to the to box left edge', () => {
    const [p] = edgePaths(boxes, [edge({})], scale);
    expect(p.d).toBe('M50,5 H58 V25 H80');
    expect(p.endX).toBe(80);
  });

  it('ends a blocked edge at the unblocked x', () => {
    const [p] = edgePaths(boxes, [edge({ kind: 'blocked', unblockedHours: 30 })], scale);
    expect(p.endX).toBe(60);
    expect(p.d.endsWith('H60')).toBe(true);
  });

  it('ends a still-blocked edge at the to box right edge', () => {
    const [p] = edgePaths(boxes, [edge({ kind: 'blocked' })], scale);
    expect(p.endX).toBe(120);
  });

  it('offsets the unblocked x by the origin', () => {
    const [p] = edgePaths(boxes, [edge({ kind: 'blocked', unblockedHours: 30 })], {
      pxPerHour: 2,
      originX: 100,
    });
    expect(p.endX).toBe(160);
  });

  it('carries a wait label only when set', () => {
    const [a, b] = edgePaths(boxes, [edge({ waitLabel: '22.8 h' }), edge({ to: 'C' })], scale);
    expect(a.waitLabel).toBe('22.8 h');
    expect(a.title).toBe('A → B, waited 22.8 h');
    expect(b.waitLabel).toBeNull();
    expect(b.title).toBe('A → C');
  });

  it('EdgePaths_WhenAnEndIsAnUnknownKey_ShouldSkipTheEdge', () => {
    expect(edgePaths(boxes, [edge({ to: 'Z' }), edge({ from: 'Z' })], scale)).toEqual([]);
  });

  it('EdgePaths_WhenAnEndIsABracket_ShouldSkipTheEdge', () => {
    const marks = [...boxes, mark('S', 0, 200, 60, 'bracket')];
    const pairs = [edge({ to: 'S' }), edge({ from: 'S' })];
    expect(edgePaths(marks, [...pairs, edge({})], scale).map((p) => `${p.from}-${p.to}`)).toEqual([
      'A-B',
    ]);
  });

  it('EdgePaths_WhenTheToEndIsADiamond_ShouldEndAtItsLeftPoint', () => {
    // A diamond at x = 90 (DIAMOND_SIZE 7) on a row whose middle is y = 85.
    const marks = [...boxes, mark('D', 90, 90, 80, 'diamond')];
    const [p] = edgePaths(marks, [edge({ to: 'D' })], scale);
    expect(p.d).toBe('M50,5 H58 V85 H83');
    expect(p.endX).toBe(83);
    expect(p.horizontal).toEqual({ x0: 58, x1: 83, y: 85 });
  });

  it('EdgePaths_WhenTheFromEndIsADiamond_ShouldStartAtItsRightPoint', () => {
    const marks = [...boxes, mark('D', 20, 20, 80, 'diamond')];
    const [p] = edgePaths(marks, [edge({ from: 'D' })], scale);
    expect(p.d).toBe('M27,85 H35 V25 H80');
    expect(p.vertical).toEqual({ x: 35, y0: 25, y1: 85 });
  });

  it('EdgePaths_WhenThePairRepeats_ShouldDrawItOnce', () => {
    const paths = edgePaths(
      boxes,
      [edge({ waitLabel: '1 h' }), edge({ kind: 'blocked' }), edge({ waitLabel: '2 h' })],
      scale,
    );
    expect(paths).toHaveLength(1);
    expect(paths[0].waitLabel).toBe('1 h');
  });

  it('EdgePaths_ShouldGiveTheHorizontalAndVerticalSegments', () => {
    const [p] = edgePaths(boxes, [edge({})], scale);
    expect(p.horizontal).toEqual({ x0: 58, x1: 80, y: 25 });
    expect(p.vertical).toEqual({ x: 58, y0: 5, y1: 25 });
  });

  it('EdgePaths_WhenTheToBarStartsBeforeTheElbow_ShouldOrderTheSegmentEnds', () => {
    const [p] = edgePaths(boxes, [edge({ from: 'B', to: 'C' })], scale);
    expect(p.horizontal).toEqual({ x0: 30, x1: 128, y: 45 });
    expect(p.vertical).toEqual({ x: 128, y0: 25, y1: 45 });
  });
});
