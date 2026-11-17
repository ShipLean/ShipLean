import { Fragment } from 'react';
import type {
  InspectionPeriodDto,
  InspectionQualityStatsDto,
  InspectionQuarterDto,
} from '@shiplean/core/api/views';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { per100, SEVERITY_PARTS, severityCount } from './severity';

const cell = 'px-3 py-2 whitespace-nowrap';
const num = `${cell} text-right tabular-nums`;

function Counts({ q }: { q: InspectionQualityStatsDto | null }) {
  if (q === null)
    return (
      <td colSpan={5 + SEVERITY_PARTS.length} className={`${cell} text-ink-muted`}>
        —
      </td>
    );
  return (
    <>
      <td className={`${num} font-semibold`}>{q.escaping}</td>
      {SEVERITY_PARTS.map((part) => (
        <td key={part} className={`${num} text-[length:var(--text-sm)]`}>
          {severityCount(q.escapingBySeverity, part) || <span className="text-ink-muted">0</span>}
        </td>
      ))}
      <td className={num}>{q.weighted}</td>
      <td className={num}>{q.foundBeforeRelease}</td>
      <td className={num}>{q.mergedPrs ?? '—'}</td>
      <td data-kind="per100" className={num}>
        {per100(q.per100Prs)}
      </td>
    </>
  );
}

/** One row per period, oldest first, each quarter's roll-up after its last period (as the lead-time rows). */
export function QualityRows({
  periods,
  quarters,
  unit,
}: {
  periods: readonly InspectionPeriodDto[];
  quarters: readonly InspectionQuarterDto[];
  unit: string;
}) {
  return (
    <section className="rounded-md border border-edge bg-panel">
      <header className="flex flex-wrap items-baseline gap-2 border-b border-edge px-3 py-2">
        <h3 className="m-0 text-[length:var(--text-md)] font-semibold">By {unit}</h3>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          per 100 merged PRs counts only pull requests linked to an item
        </span>
      </header>
      <div className="overflow-x-auto">
        <table data-testid={ids.qualityPeriodRows} className="w-full border-collapse">
          <thead className="text-left text-[length:var(--text-sm)] text-ink-muted">
            <tr className="border-b border-edge">
              <th className={`${cell} font-medium`}>{unit === 'week' ? 'Week' : 'Sprint'}</th>
              <th className={`${cell} font-medium`}>Dates</th>
              <th className={`${num} font-medium`}>Escaping</th>
              <th className={`${num} font-medium`}>Sev 1</th>
              <th className={`${num} font-medium`}>Sev 2</th>
              <th className={`${num} font-medium`}>Sev 3</th>
              <th className={`${num} font-medium`}>Sev 4</th>
              <th className={`${num} font-medium`}>No sev</th>
              <th className={`${num} font-medium`}>Weighted</th>
              <th className={`${num} font-medium`}>Found before release</th>
              <th className={`${num} font-medium`}>Merged PRs</th>
              <th className={`${num} font-medium`}>Per 100 PRs</th>
            </tr>
          </thead>
          <tbody>
            {periods.map((period, i) => {
              const next = periods[i + 1];
              const quarter =
                next === undefined || next.quarter !== period.quarter
                  ? quarters.find((q) => q.quarter === period.quarter)
                  : undefined;
              return (
                <Fragment key={period.id}>
                  <tr
                    data-testid={ids.qualityPeriodRow}
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
                    <Counts q={period.quality} />
                  </tr>
                  {quarter !== undefined && (
                    <tr
                      data-testid={ids.qualityQuarterRow}
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
                      <Counts q={quarter.quality} />
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
