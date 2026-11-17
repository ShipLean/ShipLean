import type { InspectionPeriodCostDto, InspectionPeriodDto } from '@shiplean/core/api/views';
import { inspectionMetricsTestIds as ids } from '../testIds';

/** The plot's height in px; each bar is a share of it against the tallest. */
const PLOT = 160;

/**
 * Mean tokens per task per period (design costChart): one bar each, the server's label above it, the period's short name
 * and how many tasks had runs below. A running period is dashed; a period from before the store kept runs has no bar and
 * reads "—".
 */
export function CostChart({
  periods,
  costs,
}: {
  periods: readonly InspectionPeriodDto[];
  costs: readonly InspectionPeriodCostDto[];
}) {
  const rows = periods.flatMap((period) => {
    const cost = costs.find((c) => c.periodId === period.id);
    return cost === undefined ? [] : [{ period, stats: cost.stats }];
  });
  const max = Math.max(1, ...rows.map((r) => r.stats.tokensPerTask ?? 0));
  return (
    <div>
      <div
        data-testid={ids.costChart}
        role="img"
        aria-label="Mean tokens per task per period"
        className="flex items-end gap-2 border-b border-edge"
        style={{ height: PLOT + 24 }}
      >
        {rows.map(({ period, stats }) => (
          <div
            key={period.id}
            data-testid={ids.costChartBar}
            data-period-id={period.id}
            title={`${period.name}: ${stats.tokensPerTaskLabel} per task`}
            className="flex flex-1 flex-col items-center justify-end"
          >
            {stats.kept ? (
              <>
                <b className="mb-1 text-[length:var(--text-xs)] tabular-nums">
                  {stats.tokensPerTaskLabel}
                </b>
                <span
                  className={`block w-full max-w-10 rounded-t-[3px] border ${
                    period.inProgress ? 'border-dashed' : ''
                  }`}
                  style={{
                    borderColor: 'var(--type-feature)',
                    height: ((stats.tokensPerTask ?? 0) / max) * PLOT,
                    background: period.inProgress
                      ? undefined
                      : 'color-mix(in srgb, var(--type-feature) 45%, transparent)',
                  }}
                />
              </>
            ) : (
              <span
                data-testid={ids.costNotKept}
                title="Before the store kept agent runs"
                className="mb-1 text-[length:var(--text-xs)] text-ink-muted"
              >
                —
              </span>
            )}
          </div>
        ))}
      </div>
      <div className="flex gap-2 pt-1">
        {rows.map(({ period, stats }) => (
          <div
            key={period.id}
            className="flex flex-1 flex-col items-center text-[length:var(--text-xs)] tabular-nums"
          >
            <span className="font-medium">
              {period.shortName}
              {period.inProgress && <span className="text-ink-muted"> · so far</span>}
            </span>
            <span className="text-ink-muted">
              {stats.kept ? `${stats.tasksWithRuns} with runs` : 'not kept'}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
