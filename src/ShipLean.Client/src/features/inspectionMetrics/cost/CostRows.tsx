import { Fragment } from 'react';
import type {
  InspectionCostStatsDto,
  InspectionPeriodCostDto,
  InspectionPeriodDto,
  InspectionQuarterCostDto,
} from '@shiplean/core/api/views';
import { inspectionMetricsTestIds as ids } from '../testIds';

const cell = 'px-3 py-2 whitespace-nowrap';
const num = `${cell} text-right tabular-nums`;

function Stats({ stats }: { stats: InspectionCostStatsDto }) {
  if (!stats.kept)
    return (
      <>
        <td className={num}>{stats.tasks}</td>
        <td
          data-testid={ids.costNotKept}
          colSpan={5}
          className={`${cell} text-[length:var(--text-sm)] text-ink-muted`}
        >
          — before the store kept agent runs
        </td>
      </>
    );
  return (
    <>
      <td className={num}>{stats.tasks}</td>
      <td className={num}>{stats.tasksWithRuns}</td>
      <td data-kind="perTask" className={`${num} font-semibold`}>
        {stats.tokensPerTaskLabel}
      </td>
      <td className={num}>{stats.tokensTotalLabel}</td>
      <td className={num}>{stats.mergedPrs ?? '—'}</td>
      <td data-kind="perMergedPr" className={num}>
        {stats.tokensPerMergedPrLabel}
      </td>
    </>
  );
}

/** One row per period, oldest first, each quarter's roll-up after its last period (as the quality rows). */
export function CostRows({
  periods,
  costs,
  quarters,
  unit,
}: {
  periods: readonly InspectionPeriodDto[];
  costs: readonly InspectionPeriodCostDto[];
  quarters: readonly InspectionQuarterCostDto[];
  unit: string;
}) {
  const shown = periods.flatMap((period) => {
    const cost = costs.find((c) => c.periodId === period.id);
    return cost === undefined ? [] : [{ period, stats: cost.stats }];
  });
  return (
    <section className="rounded-md border border-edge bg-panel">
      <header className="flex flex-wrap items-baseline gap-2 border-b border-edge px-3 py-2">
        <h3 className="m-0 text-[length:var(--text-md)] font-semibold">By {unit}</h3>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          the mean is over tasks with agent runs only
        </span>
      </header>
      <div className="overflow-x-auto">
        <table data-testid={ids.costPeriodRows} className="w-full border-collapse">
          <thead className="text-left text-[length:var(--text-sm)] text-ink-muted">
            <tr className="border-b border-edge">
              <th className={`${cell} font-medium`}>{unit === 'week' ? 'Week' : 'Sprint'}</th>
              <th className={`${cell} font-medium`}>Dates</th>
              <th className={`${num} font-medium`}>Tasks</th>
              <th className={`${num} font-medium`}>With runs</th>
              <th className={`${num} font-medium`}>Tokens per task</th>
              <th className={`${num} font-medium`}>Total</th>
              <th className={`${num} font-medium`}>Merged PRs</th>
              <th className={`${num} font-medium`}>Per merged PR</th>
            </tr>
          </thead>
          <tbody>
            {shown.map(({ period, stats }, i) => {
              const next = shown[i + 1];
              const quarter =
                next === undefined || next.period.quarter !== period.quarter
                  ? quarters.find((q) => q.quarter === period.quarter)
                  : undefined;
              return (
                <Fragment key={period.id}>
                  <tr
                    data-testid={ids.costPeriodRow}
                    data-period-id={period.id}
                    data-in-progress={period.inProgress || undefined}
                    className="border-b border-edge"
                  >
                    <td className={`${cell} font-semibold`}>
                      {period.name}
                      {period.inProgress && (
                        <span className="text-[length:var(--text-xs)] font-normal text-ink-muted">
                          {' '}
                          · so far
                        </span>
                      )}
                    </td>
                    <td className={`${cell} text-[length:var(--text-sm)] text-ink-muted`}>
                      {period.datesLabel}
                    </td>
                    <Stats stats={stats} />
                  </tr>
                  {quarter !== undefined && (
                    <tr
                      data-testid={ids.costQuarterRow}
                      data-quarter={quarter.quarter}
                      className="border-b border-edge bg-sunken font-semibold"
                    >
                      <td className={cell}>{quarter.quarter}</td>
                      <td
                        className={`${cell} text-[length:var(--text-sm)] font-normal text-ink-muted`}
                      >
                        the quarter&apos;s {unit}s
                      </td>
                      <Stats stats={quarter.stats} />
                    </tr>
                  )}
                </Fragment>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}
