import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import type { BarBox } from '../BarBox';
import type { TimelineOptions } from '../TimelineOptions';
import type { TimelineRow } from './timelineRows';

export const ROW_HEIGHT = 30;
export const AXIS_HEIGHT = 40;
export const BAR_HEIGHT = 14;
export const BRACKET_HEIGHT = 8;
export const DIAMOND_SIZE = 7;
/** A bar or bracket is never drawn narrower than this. */
export const MIN_BAR = 3;
export const REVIEW_DOT_RADIUS = 4;
/** Days zoom: a 24-hour day is this wide, and the chart scrolls sideways. */
export const DAY_WIDTH = 96;
export const FIT_FALLBACK_WIDTH = 960;
/** Fit labels weeks instead of days once a day is narrower than this. */
export const WEEK_LABEL_BELOW = 28;

export interface DayColumn {
  x: number;
  width: number;
  weekend: boolean;
  label: string;
  dayOfMonth: string;
}
/** One axis label: a day (centred) or a week start (left-aligned beside a tick). */
export interface AxisLabel {
  x: number;
  text: string;
  kind: 'day' | 'week';
}
export interface SprintBand {
  name: string;
  x: number;
  width: number;
}
export interface ReviewDot {
  x: number;
  verdict: 'approved' | 'changesRequested';
  atLabel: string;
}
export type MarkKind = 'bar' | 'bracket' | 'diamond';
export interface Mark {
  key: string;
  kind: MarkKind;
  row: number;
  /** The mark's box: the diamond has x0 = x1. */
  box: BarBox;
  reviews: ReviewDot[];
  /** Where the dashed line from the bar's end to the last review dot starts and ends. */
  reviewLine: { x0: number; x1: number } | null;
  /** The text of the mark's duration label (the server's durationLabel); null for a diamond and while unknown. */
  durationLabel: string | null;
}
export interface TimelineGeometry {
  pxPerHour: number;
  width: number;
  height: number;
  days: DayColumn[];
  axisLabels: AxisLabel[];
  sprints: SprintBand[];
  nowX: number | null;
  marks: Mark[];
  boxes: BarBox[];
}

export function rowY(row: number): number {
  return AXIS_HEIGHT + row * ROW_HEIGHT;
}

/** Fit divides the available width over the scale; Days gives each 24 hours DAY_WIDTH pixels. */
export function pxPerHourFor(
  zoom: TimelineOptions['zoom'],
  endHours: number,
  availableWidth: number,
): number {
  if (zoom === 'days') return DAY_WIDTH / 24;
  return endHours > 0 ? availableWidth / endHours : 0;
}

/**
 * The axis labels: one per day, or, when a day is narrower than WEEK_LABEL_BELOW, the server's shortLabel on each
 * week start (the first day when the range has none). No date maths: the choice is by weekStart.
 */
export function axisLabels(
  view: FeatureTimelineViewDto,
  days: readonly DayColumn[],
  zoom: TimelineOptions['zoom'],
): AxisLabel[] {
  const source = view.scale.days;
  const narrow = zoom === 'fit' && days.length > 0 && days[0].width < WEEK_LABEL_BELOW;
  if (!narrow) {
    return days.map((day) => ({
      x: day.x + day.width / 2,
      text: zoom === 'days' ? day.label : day.dayOfMonth,
      kind: 'day' as const,
    }));
  }
  const starts = source.flatMap((day, i) => (day.weekStart ? [i] : []));
  const picked = starts.length > 0 ? starts : source.length > 0 ? [0] : [];
  return picked.map((i) => ({ x: days[i].x, text: source[i].shortLabel, kind: 'week' as const }));
}

export function timelineGeometry(
  view: FeatureTimelineViewDto,
  rows: readonly TimelineRow[],
  zoom: TimelineOptions['zoom'],
  availableWidth: number,
): TimelineGeometry {
  const { scale } = view;
  const px = pxPerHourFor(zoom, scale.endHours, availableWidth);
  const x = (hours: number) => hours * px;
  const nowHours = scale.nowHours ?? scale.endHours;

  const days = scale.days.map((day, i) => {
    const next = scale.days[i + 1]?.startHours ?? scale.endHours;
    return {
      x: x(day.startHours),
      width: x(next - day.startHours),
      weekend: day.weekend,
      label: day.label,
      dayOfMonth: day.dayOfMonth,
    };
  });
  const sprints = scale.sprints.map((s) => ({
    name: s.name,
    x: x(s.startHours),
    width: x(s.endHours - s.startHours),
  }));

  // Every row takes its y from its position; only bar and bracket rows get a mark.
  const marks: Mark[] = [];
  rows.forEach((row, index) => {
    const y = rowY(index);
    if (row.kind === 'bracket') {
      const { story } = row;
      if (story.startHours === null) return;
      const box = {
        key: row.key,
        x0: x(story.startHours),
        x1: x(story.endHours ?? nowHours),
        y: y + (ROW_HEIGHT - BRACKET_HEIGHT) / 2,
        height: BRACKET_HEIGHT,
      };
      marks.push({
        key: row.key,
        kind: 'bracket',
        row: index,
        box,
        reviews: [],
        reviewLine: null,
        durationLabel: story.durationLabel,
      });
    } else if (row.kind === 'bar') {
      const { bar } = row;
      const reviews = bar.reviews.map((r) => ({
        x: x(r.atHours),
        verdict: r.verdict,
        atLabel: r.atLabel,
      }));
      if (bar.startHours === null) {
        if (bar.endHours === null) return;
        const cx = x(bar.endHours);
        const box = {
          key: row.key,
          x0: cx,
          x1: cx,
          y: y + (ROW_HEIGHT - BAR_HEIGHT) / 2,
          height: BAR_HEIGHT,
        };
        marks.push({
          key: row.key,
          kind: 'diamond',
          row: index,
          box,
          reviews,
          reviewLine: null,
          durationLabel: null,
        });
        return;
      }
      const x1 = x(bar.endHours ?? nowHours);
      const box = {
        key: row.key,
        x0: x(bar.startHours),
        x1,
        y: y + (ROW_HEIGHT - BAR_HEIGHT) / 2,
        height: BAR_HEIGHT,
      };
      const last = reviews.length > 0 ? Math.max(...reviews.map((r) => r.x)) : null;
      marks.push({
        key: row.key,
        kind: 'bar',
        row: index,
        box,
        reviews,
        reviewLine: last === null ? null : { x0: x1, x1: last },
        durationLabel: bar.durationLabel,
      });
    }
  });

  return {
    pxPerHour: px,
    width: x(scale.endHours),
    height: AXIS_HEIGHT + rows.length * ROW_HEIGHT,
    days,
    axisLabels: axisLabels(view, days, zoom),
    sprints,
    nowX: scale.nowHours === null ? null : x(scale.nowHours),
    marks,
    boxes: marks.map((m) => m.box),
  };
}
