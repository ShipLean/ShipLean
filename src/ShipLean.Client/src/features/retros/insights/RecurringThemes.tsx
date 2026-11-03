import { RecurringCallout } from './recurring/RecurringCallout';
import { RecurringRow } from './recurring/RecurringRow';
import { UnthemedRow } from './recurring/UnthemedRow';
import { LastChangeHeading } from './tips/LastChangeHeading';
import { NoSprintTip } from './tips/NoSprintTip';
import { StatusHeading } from './tips/StatusHeading';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';
import { retrosTestIds as ids } from '../testIds';

const HEAD = 'px-3 py-2 text-left text-[length:var(--text-xs)] font-semibold text-ink-muted';

/** Recurring themes: a table per theme with mentions per sprint or week (heat-shaded), No sprint, then Unthemed, total, status, action items, last change. */
export function RecurringThemes({ project, insights }: RetroInsightsSlotProps) {
  const { columns, themes, mode } = insights;
  const max = Math.max(0, ...themes.flatMap((t) => t.counts));
  const first = columns[0]?.label;
  const last = columns[columns.length - 1]?.label.replace(/^Sprint\s+/, '');
  const recurring = themes.filter((t) => t.status === 'recurring').length;
  return (
    <div className="flex flex-col gap-3">
      <RecurringCallout count={recurring} mode={mode} weeks={columns.length} />
      <section className="rounded-frame border border-edge bg-panel">
        <header className="flex items-baseline gap-3 px-4 py-3">
          <h2 className="m-0 text-[length:var(--text-md)] font-semibold">
            Went badly and not in the plan
          </h2>
          <span className="text-[length:var(--text-sm)] text-ink-muted">
            by theme{first && last ? `, ${first} – ${last}` : ''}
          </span>
        </header>
        <div className="overflow-x-auto">
          <table data-testid={ids.retroRecurringTable} className="w-full border-collapse">
            <thead>
              <tr>
                <th className={HEAD}>Theme</th>
                {columns.map((column) => (
                  <th key={column.key} className={`${HEAD} text-center`}>
                    {column.label}
                  </th>
                ))}
                {mode === 'sprint' ? (
                  <th
                    data-testid={ids.retroInsightsNosprint}
                    className={`${HEAD} border-l-2 border-edge text-center`}
                  >
                    <NoSprintTip />
                  </th>
                ) : null}
                <th className={`${HEAD} text-right`}>Total</th>
                <StatusHeading className={HEAD} mode={insights.mode} />
                <th className={HEAD}>Action items</th>
                <LastChangeHeading className={HEAD} mode={insights.mode} />
              </tr>
            </thead>
            <tbody>
              {themes.map((row) => (
                <RecurringRow
                  key={row.theme.id}
                  projectId={project.id}
                  row={row}
                  columns={columns}
                  max={max}
                  mode={mode}
                />
              ))}
              <UnthemedRow
                projectKey={project.key}
                unthemed={insights.unthemed}
                columns={columns}
                max={max}
                mode={mode}
              />
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
