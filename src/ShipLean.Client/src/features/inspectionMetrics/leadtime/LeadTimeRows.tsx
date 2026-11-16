import { Fragment } from 'react';
import type {
  InspectionPeriodDto,
  InspectionQuarterDto,
  LeadTimeStatsDto,
} from '@shiplean/core/api/views';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { ChangeChip } from './ChangeChip';
import { SplitBar, SplitLegend } from './SplitBar';

export interface LeadTimeRowsProps {
  periods: readonly InspectionPeriodDto[];
  quarters: readonly InspectionQuarterDto[];
  /** "sprint" or "week". */
  unit: string;
  onPeriod(periodId: string): void;
}

const cell = 'px-3 py-2 whitespace-nowrap';
const num = `${cell} text-right tabular-nums`;

function MeanCell({ stats, max }: { stats: LeadTimeStatsDto; max: number }) {
  return (
    <td className={`${cell} w-[30%]`}>
      <div className="grid grid-cols-[54px_minmax(60px,1fr)] items-center gap-2">
        <b className="text-right text-[length:var(--text-sm)] tabular-nums">
          {stats.meanLabel ?? '—'}
        </b>
        <SplitBar stats={stats} max={max} />
      </div>
    </td>
  );
}

function Parts({ stats }: { stats: LeadTimeStatsDto }) {
  return (
    <>
      <td className={`${num} text-[length:var(--text-sm)]`}>{stats.meanActiveLabel ?? '—'}</td>
      <td className={`${num} text-[length:var(--text-sm)]`}>{stats.meanBlockedLabel ?? '—'}</td>
      <td className={`${num} text-[length:var(--text-sm)]`}>{stats.meanReworkLabel ?? '—'}</td>
    </>
  );
}

/** One row per period, oldest first, with each quarter's roll-up after its last period. */
export function LeadTimeRows({ periods, quarters, unit, onPeriod }: LeadTimeRowsProps) {
  const max = Math.max(
    0,
    ...periods.map((p) => p.leadTime.meanHours ?? 0),
    ...quarters.map((q) => q.leadTime.meanHours ?? 0),
  );
  return (
    <section className="rounded-md border border-edge bg-panel">
      <header className="flex flex-wrap items-baseline gap-2 border-b border-edge px-3 py-2">
        <h3 className="m-0 text-[length:var(--text-md)] font-semibold">By {unit}</h3>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          each part is a mean too, so active + blocked + rework = the lead time
        </span>
        <span className="ml-auto">
          <SplitLegend />
        </span>
      </header>
      <table data-testid={ids.periodRows} className="w-full border-collapse">
        <thead className="text-left text-[length:var(--text-sm)] text-ink-muted">
          <tr className="border-b border-edge">
            <th className={`${cell} font-medium`}>{unit === 'week' ? 'Week' : 'Sprint'}</th>
            <th className={`${cell} font-medium`}>Dates</th>
            <th className={`${num} font-medium`}>Tasks</th>
            <th className={`${cell} font-medium`}>Mean lead time</th>
            <th className={`${num} font-medium`}>Active</th>
            <th className={`${num} font-medium`}>Blocked</th>
            <th className={`${num} font-medium`}>Rework</th>
            <th className={`${cell} font-medium`}>vs previous</th>
            <th
              className={`${num} font-medium`}
              title="Tasks with no commit time, whose lead time ends at Resolved"
            >
              Ended at Resolved
            </th>
          </tr>
        </thead>
        <tbody>
          {periods.map((period, i) => {
            const next = periods[i + 1];
            const quarter =
              next === undefined || next.quarter !== period.quarter
                ? quarters.find((q) => q.quarter === period.quarter)
                : undefined;
            const stats = period.leadTime;
            return (
              <Fragment key={period.id}>
                <tr
                  data-testid={ids.periodRow}
                  data-period-id={period.id}
                  data-in-progress={period.inProgress || undefined}
                  className="cursor-pointer border-b border-edge hover:bg-sunken"
                  onClick={() => onPeriod(period.id)}
                >
                  <td className={cell}>
                    <button
                      type="button"
                      className="font-semibold text-ink"
                      onClick={(event) => {
                        event.stopPropagation();
                        onPeriod(period.id);
                      }}
                    >
                      {period.name}
                    </button>
                    {period.inProgress && (
                      <span className="text-[length:var(--text-xs)] text-ink-muted"> · so far</span>
                    )}
                  </td>
                  <td className={`${cell} text-[length:var(--text-sm)] text-ink-muted`}>
                    {period.datesLabel}
                  </td>
                  <td className={num}>{stats.tasks}</td>
                  <MeanCell stats={stats} max={max} />
                  <Parts stats={stats} />
                  <td className={cell}>
                    <ChangeChip pct={stats.changePctVsPrevious} unit={unit} />
                  </td>
                  <td className={`${num} text-[length:var(--text-sm)]`}>
                    {stats.endedAtResolved > 0 ? (
                      <span
                        className="font-semibold text-caution"
                        title="No commit time: lead time ends at the first move to Resolved"
                      >
                        {stats.endedAtResolved}
                      </span>
                    ) : (
                      <span className="text-ink-muted">0</span>
                    )}
                  </td>
                </tr>
                {quarter !== undefined && (
                  <tr
                    data-testid={ids.quarterRow}
                    data-quarter={quarter.quarter}
                    data-to-date={quarter.toDate || undefined}
                    className="border-b border-edge bg-sunken font-semibold"
                  >
                    <td className={cell}>
                      {quarter.quarter}
                      {quarter.toDate && (
                        <span className="font-normal text-ink-muted italic"> to date</span>
                      )}
                    </td>
                    <td
                      className={`${cell} text-[length:var(--text-sm)] font-normal text-ink-muted`}
                    >
                      all {quarter.periodIds.length} {unit}s in the quarter
                    </td>
                    <td className={num}>{quarter.leadTime.tasks}</td>
                    <MeanCell stats={quarter.leadTime} max={max} />
                    <Parts stats={quarter.leadTime} />
                    <td className={cell} />
                    <td className={`${num} text-[length:var(--text-sm)]`}>
                      {quarter.leadTime.endedAtResolved}
                    </td>
                  </tr>
                )}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </section>
  );
}
