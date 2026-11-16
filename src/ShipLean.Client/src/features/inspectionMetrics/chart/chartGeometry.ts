import type { InspectionPeriodDto } from '@shiplean/core/api/views';

// The lead-time chart's layout (SHIP-1398 K12, design/screens/inspection.html leadChart): one bar per period at its
// mean, one dot per task, on a y axis that tops out at 2 days. Works on hours; every value label is the server's.

/** The axis tops out at 2 days; a task over it sits on the top edge in red. */
export const AXIS_MAX_HOURS = 48;
export const CHART_HEIGHT = 220;
export const FALLBACK_WIDTH = 900;
export const MIN_WIDTH = 320;
const LEFT = 38;
const BOTTOM = 34;
const TOP = 16;
const MAX_BAR_WIDTH = 46;
const DOT_RADIUS = 2.6;
const OVER_DOT_RADIUS = 3.6;

const TICKS: readonly { hours: number; label: string }[] = [
  { hours: 0, label: '0' },
  { hours: 12, label: '12 h' },
  { hours: 24, label: '1 d' },
  { hours: 36, label: '36 h' },
  { hours: 48, label: '2 d' },
];

export interface ChartTick {
  y: number;
  label: string;
  /** The zero line is drawn heavier. */
  base: boolean;
}

export interface ChartBar {
  periodId: string;
  name: string;
  shortName: string;
  tasks: number;
  /** The period's centre. */
  cx: number;
  x: number;
  width: number;
  /** The mean's top edge; null when the period has no tasks with a lead time. */
  top: number | null;
  bottom: number;
  meanLabel: string | null;
  inProgress: boolean;
  /** The click area: the whole column of the period. */
  hitX: number;
  hitWidth: number;
}

export interface ChartDot {
  taskKey: string;
  title: string;
  periodId: string;
  cx: number;
  cy: number;
  r: number;
  /** Over 2 days: clamped to the top edge and drawn in red. */
  over: boolean;
  longest: boolean;
  leadLabel: string;
  endedBy: InspectionPeriodDto['dots'][number]['endedBy'];
}

export interface ChartQuarter {
  x: number;
  label: string;
  /** A dashed divider where the quarter changes (not before the first period). */
  divider: boolean;
}

export interface LeadTimeLayout {
  empty: boolean;
  width: number;
  height: number;
  top: number;
  left: number;
  baseY: number;
  periodLabelY: number;
  tasksLabelY: number;
  ticks: ChartTick[];
  bars: ChartBar[];
  dots: ChartDot[];
  quarters: ChartQuarter[];
}

export function isOverAxis(hours: number): boolean {
  return hours > AXIS_MAX_HOURS;
}

export function leadTimeLayout(
  periods: readonly InspectionPeriodDto[],
  availableWidth: number,
  height: number = CHART_HEIGHT,
): LeadTimeLayout {
  const width = Math.max(MIN_WIDTH, Math.round(availableWidth) || FALLBACK_WIDTH);
  const plot = height - TOP - BOTTOM;
  const y = (hours: number) => TOP + (1 - Math.min(hours, AXIS_MAX_HOURS) / AXIS_MAX_HOURS) * plot;
  const baseY = y(0);
  const layout: LeadTimeLayout = {
    empty: periods.length === 0,
    width,
    height,
    top: TOP,
    left: LEFT,
    baseY,
    periodLabelY: height - BOTTOM + 15,
    tasksLabelY: height - BOTTOM + 28,
    ticks: TICKS.map((t) => ({ y: y(t.hours), label: t.label, base: t.hours === 0 })),
    bars: [],
    dots: [],
    quarters: [],
  };
  if (layout.empty) return layout;

  const column = (width - LEFT) / periods.length;
  const barWidth = Math.min(MAX_BAR_WIDTH, column * 0.5);
  periods.forEach((period, i) => {
    const columnX = LEFT + column * i;
    const cx = columnX + column / 2;
    const previous = periods[i - 1];
    if (previous === undefined || previous.quarter !== period.quarter)
      layout.quarters.push({
        x: columnX + 4,
        label: period.quarter,
        divider: previous !== undefined,
      });
    const mean = period.leadTime.meanHours;
    layout.bars.push({
      periodId: period.id,
      name: period.name,
      shortName: period.shortName,
      tasks: period.leadTime.tasks,
      cx,
      x: cx - barWidth / 2,
      width: barWidth,
      top: mean === null ? null : y(mean),
      bottom: baseY,
      meanLabel: period.leadTime.meanLabel,
      inProgress: period.inProgress,
      hitX: columnX + 2,
      hitWidth: column - 4,
    });
    period.dots.forEach((dot, j) => {
      // The same spread as the design: a fixed pseudo-random jitter so dots of one period don't sit on one line.
      const jitter = ((((j * 37) % 11) - 5) / 5) * barWidth * 0.42;
      const over = isOverAxis(dot.leadHours);
      layout.dots.push({
        taskKey: dot.taskKey,
        title: dot.title,
        periodId: period.id,
        cx: cx + jitter,
        cy: over ? TOP + 2 : y(dot.leadHours),
        r: over ? OVER_DOT_RADIUS : DOT_RADIUS,
        over,
        longest: dot.longest,
        leadLabel: dot.leadLabel,
        endedBy: dot.endedBy,
      });
    });
  });
  return layout;
}
