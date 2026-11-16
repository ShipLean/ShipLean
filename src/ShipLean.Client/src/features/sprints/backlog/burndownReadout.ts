import type { BurndownDot } from './burndownGeometry';

/** The day a tap or drag at `x` (in the chart's own units) points at: the closest actual point, so a drag past either end stays on the end. */
export function burndownDotAt(dots: readonly BurndownDot[], x: number): BurndownDot | undefined {
  let nearest: BurndownDot | undefined;
  for (const dot of dots) {
    if (nearest === undefined || Math.abs(dot.x - x) < Math.abs(nearest.x - x)) nearest = dot;
  }
  return nearest;
}

/** What the burndown's header reads for a day: the date, the points left and the ideal for that day. */
export function burndownReadout(dot: BurndownDot): string {
  return `${dot.label} · ${dot.remaining} left · ideal ${dot.ideal}`;
}
