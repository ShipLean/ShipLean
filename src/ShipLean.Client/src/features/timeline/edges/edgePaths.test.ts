import { describe, expect, it } from 'vitest';
import type { TimelineEdgeDto } from '@shiplean/core/api/views';
import { edgePaths } from './edgePaths';

const box = (key: string, x0: number, x1: number, y: number) => ({ key, x0, x1, y, height: 10 });
const boxes = [box('A', 10, 50, 0), box('B', 80, 120, 20), box('C', 30, 60, 40)];
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

  it('skips an edge whose box is missing', () => {
    expect(edgePaths(boxes, [edge({ to: 'Z' }), edge({ from: 'Z' })], scale)).toEqual([]);
  });
});
