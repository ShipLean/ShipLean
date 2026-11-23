import type { TimelineEdgeDto } from '@shiplean/core/api/views';
import type { Mark } from '../chart/timelineGeometry';

export interface EdgeScale {
  pxPerHour: number;
  originX: number;
}

/** One drawn edge: an elbow path, its arrow head, and the segments a wait label may sit next to. */
export interface EdgePath {
  from: string;
  to: string;
  kind: TimelineEdgeDto['kind'];
  d: string;
  head: string;
  endX: number;
  endY: number;
  /** The long horizontal segment into the to bar (x0 ≤ x1). */
  horizontal: { x0: number; x1: number; y: number };
  /** The elbow's vertical segment (y0 ≤ y1). */
  vertical: { x: number; y0: number; y1: number };
  waitLabel: string | null;
  onCriticalPath: boolean;
  title: string;
}

const ELBOW = 8;
const HEAD = 5;

/** The key of an edge's (from, to) pair: one arrow is drawn per pair. */
export const edgePairKey = (edge: { from: string; to: string }): string =>
  `${edge.from}→${edge.to}`;

/**
 * The edges whose two ends are drawn bars, in order, one per (from, to) pair (the first one the data gives).
 * An edge into or out of a bracket, a diamond or an item with no mark is skipped.
 */
export function edgePaths(marks: Mark[], edges: TimelineEdgeDto[], scale: EdgeScale): EdgePath[] {
  const bars = new Map(marks.filter((m) => m.kind === 'bar').map((m) => [m.key, m.box]));
  const seen = new Set<string>();
  const paths: EdgePath[] = [];
  for (const edge of edges) {
    const from = bars.get(edge.from);
    const to = bars.get(edge.to);
    const pair = edgePairKey(edge);
    if (!from || !to || seen.has(pair)) continue;
    seen.add(pair);
    const x1 = from.x1;
    const y1 = from.y + from.height / 2;
    const y2 = to.y + to.height / 2;
    const x2 =
      edge.kind === 'blocked' && edge.unblockedHours !== null
        ? scale.originX + edge.unblockedHours * scale.pxPerHour
        : edge.kind === 'blocked'
          ? to.x1
          : to.x0;
    const mx = x1 + ELBOW;
    paths.push({
      from: edge.from,
      to: edge.to,
      kind: edge.kind,
      d: `M${x1},${y1} H${mx} V${y2} H${x2}`,
      head: `M${x2},${y2} l${-HEAD},${-HEAD / 2 - 1} v${HEAD + 2} z`,
      endX: x2,
      endY: y2,
      horizontal: { x0: Math.min(mx, x2), x1: Math.max(mx, x2), y: y2 },
      vertical: { x: mx, y0: Math.min(y1, y2), y1: Math.max(y1, y2) },
      waitLabel: edge.waitLabel,
      onCriticalPath: edge.onCriticalPath,
      title: `${edge.from} → ${edge.to}${edge.waitLabel ? `, waited ${edge.waitLabel}` : ''}`,
    });
  }
  return paths;
}
