export const IMPACT_WIDTH = 320;
export const IMPACT_HEIGHT = 108;
const LEFT = 18;
const TOP = 12;
const BOTTOM = 18;
const MIN_TOP = 4;

export interface ImpactBar {
  /** Index into the view's sprints. */
  index: number;
  count: number;
  x: number;
  y: number;
  width: number;
  height: number;
  /** True for a sprint after the one the action closed in. */
  after: boolean;
  /** Where the sprint's label is centred. */
  labelX: number;
}

export interface ImpactLine {
  x1: number;
  x2: number;
  y: number;
  value: number;
}

export interface ImpactShape {
  /** The scale's top: an even number, at least 4, covering every bar and average. */
  max: number;
  baselineY: number;
  bars: ImpactBar[];
  /** Gridlines at 0, half and the top. */
  ticks: { value: number; y: number }[];
  /** The x of the closed marker; the left edge when the action closed before the shown range. */
  markerX: number;
  before: ImpactLine | null;
  after: ImpactLine | null;
}

/** A column's short name: "Sprint 22" is "S22"; a week's label is kept. */
export const shortColumnLabel = (label: string) => label.replace(/^Sprint\s+/, 'S');

const round = (n: number) => Math.round(n * 100) / 100;

/**
 * Maps an impact card's sprints onto its chart box. Bars are mentions per sprint; the marker sits on the right edge of the
 * sprint the action closed in (`closedAt`, -1 = before the range, so the left edge); the before line runs from the left edge to
 * the marker at `beforeAvg`, the after line from the marker to the right edge at `afterAvg` (none when it is null).
 */
export function impactShape(
  counts: readonly number[],
  closedAt: number,
  beforeAvg: number,
  afterAvg: number | null,
): ImpactShape {
  const top = Math.max(
    MIN_TOP,
    Math.ceil(Math.max(0, ...counts, beforeAvg, afterAvg ?? 0) / 2) * 2,
  );
  const baselineY = IMPACT_HEIGHT - BOTTOM;
  const y = (value: number) => round(baselineY - (value / top) * (baselineY - TOP));
  const slot = (IMPACT_WIDTH - LEFT) / Math.max(counts.length, 1);
  const marker = Math.min(Math.max(closedAt, -1), counts.length - 1);
  const markerX = round(LEFT + (marker + 1) * slot);
  return {
    max: top,
    baselineY,
    bars: counts.map((count, index) => {
      const barY = y(count);
      return {
        index,
        count,
        x: round(LEFT + index * slot + 5),
        y: barY,
        width: round(Math.max(slot - 10, 1)),
        height: Math.max(round(baselineY - barY), 1),
        after: index > closedAt,
        labelX: round(LEFT + index * slot + slot / 2),
      };
    }),
    ticks: [0, top / 2, top].map((value) => ({ value, y: y(value) })),
    markerX,
    before: markerX > LEFT ? { x1: LEFT, x2: markerX, y: y(beforeAvg), value: beforeAvg } : null,
    after:
      afterAvg === null ? null : { x1: markerX, x2: IMPACT_WIDTH, y: y(afterAvg), value: afterAvg },
  };
}
