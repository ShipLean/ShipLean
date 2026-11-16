import type { LeadTimeStatsDto } from '@shiplean/core/api/views';
import { inspectionMetricsTestIds as ids } from '../testIds';

/** The split's colours, shared by the bar, the legend and the rule drawing (design: --in-active, --in-blocked, --in-rework). */
export const SPLIT_COLOURS = {
  active: 'var(--chart-completed)',
  blocked: 'var(--color-danger)',
  rework: 'var(--color-warn)',
} as const;

type SplitKind = keyof typeof SPLIT_COLOURS;

const hoursOf = (stats: LeadTimeStatsDto): Record<SplitKind, number> => ({
  active: stats.meanActiveHours ?? 0,
  blocked: stats.meanBlockedHours ?? 0,
  rework: stats.meanReworkHours ?? 0,
});

/**
 * One group's mean lead time as a bar `mean / max` of the cell wide, split into its active, blocked and rework means (the
 * parts add up to the mean). Nothing with no mean.
 */
export function SplitBar({ stats, max }: { stats: LeadTimeStatsDto; max: number }) {
  const mean = stats.meanHours;
  if (mean === null || max <= 0) return null;
  const hours = hoursOf(stats);
  const total = hours.active + hours.blocked + hours.rework;
  return (
    <span
      data-testid={ids.periodRowSplit}
      className="flex h-2 min-w-[2px] overflow-hidden rounded-sm"
      style={{ width: `${Math.max(2, (mean / max) * 100)}%` }}
    >
      {(Object.keys(SPLIT_COLOURS) as SplitKind[]).map((kind) => (
        <i
          key={kind}
          data-testid={ids.splitSegment}
          data-kind={kind}
          className="block h-full"
          style={{
            width: total > 0 ? `${(hours[kind] / total) * 100}%` : '0%',
            background: SPLIT_COLOURS[kind],
          }}
        />
      ))}
    </span>
  );
}

export function SplitLegend() {
  const labels: Record<SplitKind, string> = {
    active: 'Active',
    blocked: 'Blocked',
    rework: 'Rework',
  };
  return (
    <span className="inline-flex flex-wrap items-center gap-3 text-[length:var(--text-xs)] text-ink-muted">
      {(Object.keys(SPLIT_COLOURS) as SplitKind[]).map((kind) => (
        <span key={kind} className="inline-flex items-center gap-1">
          <i
            aria-hidden="true"
            className="inline-block size-2.5 rounded-sm"
            style={{ background: SPLIT_COLOURS[kind] }}
          />
          {labels[kind]}
        </span>
      ))}
    </span>
  );
}
