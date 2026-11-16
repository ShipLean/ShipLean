import type {
  InspectionCutRowDto,
  InspectionPeriodDto,
  LeadTimeStatsDto,
} from '@shiplean/core/api/views';
import { inspectionMetricsTestIds as ids } from '../testIds';

export interface LeadTimeCutTableProps {
  cut: 'feature' | 'who';
  rows: readonly InspectionCutRowDto[];
  periods: readonly InspectionPeriodDto[];
  /** "sprint" or "week". */
  unit: string;
}

const cell = 'px-3 py-2 whitespace-nowrap';
const num = `${cell} text-right tabular-nums`;

function HeatCell({ stats, max }: { stats: LeadTimeStatsDto; max: number }) {
  const mean = stats.meanHours;
  if (stats.tasks === 0 || mean === null)
    return (
      <td data-testid={ids.cutCell} className={`${cell} text-center text-ink-muted`}>
        ·
      </td>
    );
  const share = max > 0 ? Math.round(6 + (mean / max) * 44) : 6;
  return (
    <td
      data-testid={ids.cutCell}
      className={`${cell} text-center tabular-nums`}
      style={{ background: `color-mix(in srgb, var(--chart-completed) ${share}%, transparent)` }}
    >
      {stats.meanLabel}
      <small className="block text-[length:var(--text-2xs)] leading-none text-ink-muted">
        {stats.tasks}
      </small>
    </td>
  );
}

/** Mean lead time per period for each Feature, or for each person and agent, as a heat table. */
export function LeadTimeCutTable({ cut, rows, periods, unit }: LeadTimeCutTableProps) {
  const max = Math.max(0, ...rows.flatMap((row) => row.cells.map((c) => c.meanHours ?? 0)));
  return (
    <section
      data-testid={ids.cutTable}
      data-cut={cut}
      className="rounded-md border border-edge bg-panel"
    >
      <header className="flex flex-wrap items-baseline gap-2 border-b border-edge px-3 py-2">
        <h3 className="m-0 text-[length:var(--text-md)] font-semibold">
          By {cut === 'feature' ? 'Feature' : 'who did the work'}
        </h3>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          mean lead time per {unit}; the small number is how many tasks
          {cut === 'who' ? '. Agents are listed by connection, people by name' : ''}
        </span>
      </header>
      <div className="overflow-x-auto">
        <table className="w-full border-collapse">
          <thead className="text-[length:var(--text-sm)] text-ink-muted">
            <tr className="border-b border-edge">
              <th className={`${cell} text-left font-medium`}>
                {cut === 'feature' ? 'Feature' : 'Who'}
              </th>
              {periods.map((period) => (
                <th key={period.id} className={`${cell} text-center font-medium`}>
                  {period.shortName}
                </th>
              ))}
              <th className={`${num} font-medium`}>Tasks</th>
              <th className={`${num} font-medium`}>Mean</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr
                key={row.key}
                data-testid={ids.cutRow}
                data-key={row.key}
                className="border-b border-edge"
              >
                <td className={`${cell} text-[length:var(--text-sm)]`}>
                  {cut === 'feature' ? (
                    <>
                      <span className="mr-1.5 text-ink-muted">{row.key}</span>
                      {row.label}
                    </>
                  ) : (
                    <span className="inline-flex items-center gap-1.5">
                      {row.label}
                      {row.agent && (
                        <span className="rounded-sm border border-edge bg-sunken px-1 text-[length:var(--text-2xs)] text-ink-muted">
                          Agent
                        </span>
                      )}
                    </span>
                  )}
                </td>
                {row.cells.map((stats, i) => (
                  <HeatCell key={periods[i]?.id ?? i} stats={stats} max={max} />
                ))}
                <td className={num}>{row.all.tasks}</td>
                <td className={num}>
                  <b>{row.all.meanLabel ?? '—'}</b>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
