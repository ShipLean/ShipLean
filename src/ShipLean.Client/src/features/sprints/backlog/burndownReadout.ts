import type { BurndownDot } from './burndownGeometry';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

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
  const month = MONTHS[Number(dot.date.slice(5, 7)) - 1] ?? '';
  return `${dot.day} ${month} · ${dot.remaining} left · ideal ${dot.ideal}`;
}
