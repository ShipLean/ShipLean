import type { RunAxisDto, RunRowDto } from '@shiplean/core/api/inspection';

/** A horizontal placement on the track, as percentages of the axis. */
export interface Span {
  left: number;
  width: number;
}

/** The part of [startMs, endMs] inside the axis, or null when it lies outside (zoomed away). */
export function clip(axis: RunAxisDto, startMs: number, endMs: number): Span | null {
  const total = axis.endMs - axis.startMs;
  if (total <= 0) return null;
  const a = Math.max(startMs, axis.startMs);
  const b = Math.min(endMs, axis.endMs);
  if (b < a) return null;
  return { left: ((a - axis.startMs) / total) * 100, width: ((b - a) / total) * 100 };
}

/** Where a point in time sits on the axis, in percent, or null when outside it. */
export function pointPct(axis: RunAxisDto, ms: number): number | null {
  return clip(axis, ms, ms)?.left ?? null;
}

/** The row's own bar; a row with no end or no duration is a point (a diamond). */
export function rowSpan(axis: RunAxisDto, row: RunRowDto): Span | null {
  if (row.endMs === null || row.durationMs === null || row.durationMs === 0) return null;
  return clip(axis, row.startMs, row.endMs);
}

/** The bar's part after the permission wait: the wait is drawn striped at the front of the tool's bar. */
export function afterWait(axis: RunAxisDto, row: RunRowDto): Span | null {
  const from = row.waitEndMs ?? row.startMs;
  return row.endMs === null ? null : clip(axis, from, row.endMs);
}

export function waitSpan(axis: RunAxisDto, row: RunRowDto): Span | null {
  if (row.waitStartMs === null || row.waitEndMs === null) return null;
  return clip(axis, row.waitStartMs, row.waitEndMs);
}

const STEPS_S = [1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600];

/** Tick offsets (ms) for the header: about eight across the axis, none in the last 4 percent. */
export function ticks(axis: RunAxisDto): number[] {
  const span = axis.endMs - axis.startMs;
  if (span <= 0) return [];
  const step = (STEPS_S.find((s) => s * 1000 >= span / 8) ?? 3600) * 1000;
  const out: number[] = [];
  for (let t = Math.ceil(axis.startMs / step) * step; t <= axis.endMs - span * 0.04; t += step) {
    out.push(t);
  }
  return out;
}

export function formatDuration(ms: number): string {
  if (ms < 1000) return `${Math.round(ms)} ms`;
  const s = ms / 1000;
  if (s < 10) return `${s.toFixed(1)} s`;
  if (s < 60) return `${Math.round(s)} s`;
  const m = Math.floor(s / 60);
  const rest = Math.round(s - m * 60);
  return rest === 0 ? `${m} m` : `${m} m ${rest} s`;
}

export function tickLabel(ms: number): string {
  if (ms === 0) return '0';
  return ms % 60_000 === 0 ? `+${ms / 60_000} m` : `+${ms / 1000} s`;
}
