import type { BurndownPoint } from '../../../api/sprints';

export const BURNDOWN_WIDTH = 232;
export const BURNDOWN_HEIGHT = 74;
const LEFT = 24;
const RIGHT = 6;
const TOP = 6;
const BOTTOM = 16;

export interface BurndownDot {
  x: number;
  y: number;
  /** The server's yyyy-MM-dd: unique across a sprint that spans two months. */
  date: string;
  /** The day of the month. */
  day: number;
  remaining: number;
  ideal: number;
}

export interface BurndownShape {
  /** The scale's top: the most points on any line. */
  max: number;
  baselineY: number;
  dots: BurndownDot[];
  /** SVG `points` of the ideal (every working day of the sprint) and the actual line (up to today). */
  idealPoints: string;
  actualPoints: string;
}

/**
 * Maps a sprint onto the chart's box: the x-axis is every working day of the sprint, the ideal line runs from the committed
 * points on the first day to zero on the last, and each actual point sits at its own date.
 */
export function burndownShape(
  points: BurndownPoint[],
  days: readonly string[],
  committed: number,
): BurndownShape {
  const max = Math.max(1, committed, ...points.map((p) => p.remaining));
  const x = (index: number) =>
    days.length <= 1 ? LEFT : LEFT + (index * (BURNDOWN_WIDTH - LEFT - RIGHT)) / (days.length - 1);
  const y = (value: number) => TOP + (1 - value / max) * (BURNDOWN_HEIGHT - TOP - BOTTOM);
  const round = (n: number) => Math.round(n * 100) / 100;
  const idealOn = (index: number) =>
    days.length <= 1 ? 0 : (committed * (days.length - 1 - index)) / (days.length - 1);
  const dots = points.flatMap((p) => {
    const index = days.indexOf(p.date);
    if (index < 0) return [];
    return [
      {
        x: round(x(index)),
        y: round(y(p.remaining)),
        date: p.date,
        day: Number(p.date.slice(8, 10)),
        remaining: p.remaining,
        ideal: p.ideal,
      },
    ];
  });
  return {
    max,
    baselineY: BURNDOWN_HEIGHT - BOTTOM,
    dots,
    idealPoints: days.map((_, index) => `${round(x(index))},${round(y(idealOn(index)))}`).join(' '),
    actualPoints: dots.map((dot) => `${dot.x},${dot.y}`).join(' '),
  };
}
