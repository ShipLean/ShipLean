import type { TimelineEdgeDto } from '@shiplean/core/api/views';
import type { BarBox } from '../BarBox';

export interface EdgeScale {
  pxPerHour: number;
  originX: number;
}

/** One drawn edge: an elbow path, its arrow head, and where the wait label sits (null when the edge has none). */
export interface EdgePath {
  from: string;
  to: string;
  kind: TimelineEdgeDto['kind'];
  d: string;
  head: string;
  endX: number;
  endY: number;
  waitLabel: string | null;
  labelX: number;
  labelY: number;
  onCriticalPath: boolean;
  title: string;
}

const ELBOW = 8;
const HEAD = 5;

/** The edges whose two boxes are both on the chart, in order; an edge with a missing box is skipped. */
export function edgePaths(boxes: BarBox[], edges: TimelineEdgeDto[], scale: EdgeScale): EdgePath[] {
  const byKey = new Map(boxes.map((b) => [b.key, b]));
  const paths: EdgePath[] = [];
  for (const edge of edges) {
    const from = byKey.get(edge.from);
    const to = byKey.get(edge.to);
    if (!from || !to) continue;
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
      waitLabel: edge.waitLabel,
      labelX: mx + 3,
      labelY: y2 - 3,
      onCriticalPath: edge.onCriticalPath,
      title: `${edge.from} → ${edge.to}${edge.waitLabel ? `, waited ${edge.waitLabel}` : ''}`,
    });
  }
  return paths;
}
