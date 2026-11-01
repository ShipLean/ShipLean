import { RecurringRow } from './recurring/RecurringRow';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';
import { retrosTestIds as ids } from '../testIds';

const HEAD = 'px-3 py-2 text-left text-[length:var(--text-xs)] font-semibold text-ink-muted';

/** Recurring themes: a table per theme with mentions per sprint (heat-shaded), total, status, action items, last change. */
export function RecurringThemes({ project, insights }: RetroInsightsSlotProps) {
  const { sprints, themes } = insights;
  const max = Math.max(0, ...themes.flatMap((t) => t.counts));
  const first = sprints[0]?.name;
  const last = sprints[sprints.length - 1]?.name;
  return (
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
              {sprints.map((sprint) => (
                <th key={sprint.id} className={`${HEAD} text-center`}>
                  {sprint.name}
                </th>
              ))}
              <th className={`${HEAD} text-right`}>Total</th>
              <th className={HEAD}>Status</th>
              <th className={HEAD}>Action items</th>
              <th className={HEAD}>Last change</th>
            </tr>
          </thead>
          <tbody>
            {themes.map((row) => (
              <RecurringRow
                key={row.theme.id}
                projectId={project.id}
                row={row}
                sprints={sprints}
                max={max}
              />
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
