import { useProjectSprints } from '../../workItem/workItemApi';
import { retrosTestIds as ids } from '../testIds';
import { ActionRow } from './actions/ActionRow';
import { nextSprintOf } from './actions/nextSprint';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';

const HEAD = 'px-3 py-2 text-left text-[length:var(--text-xs)] font-semibold text-ink-muted';

/** Action items: every action item raised from a retro, with its sprint (or a button to pull it into the next one) and its impact. */
export function ActionItemsTable({ project, insights }: RetroInsightsSlotProps) {
  const sprints = useProjectSprints(project.id);
  const next = sprints.data ? nextSprintOf(sprints.data) : undefined;
  const { actionItems } = insights;
  const done = actionItems.filter((r) => r.action.state.category === 'completed').length;
  return (
    <section className="rounded-frame border border-edge bg-panel">
      <header className="flex items-baseline gap-3 px-4 py-3">
        <h2 className="m-0 text-[length:var(--text-md)] font-semibold">Action items</h2>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          {actionItems.length - done} open · {done} done
        </span>
      </header>
      {actionItems.length === 0 ? (
        <p className="m-0 px-4 pb-4 text-[length:var(--text-sm)] text-ink-muted">Nothing here.</p>
      ) : (
        <div className="overflow-x-auto">
          <table data-testid={ids.retroActionsTable} className="w-full border-collapse">
            <thead>
              <tr>
                <th className={HEAD}>Action</th>
                <th className={HEAD}>Theme</th>
                <th className={HEAD}>Raised</th>
                <th className={HEAD}>State</th>
                <th className={HEAD}>Sprint</th>
                <th className={`${HEAD} text-right`}>Pts</th>
                <th className={HEAD}>Owner</th>
                <th className={HEAD}>Impact</th>
              </tr>
            </thead>
            <tbody>
              {actionItems.map((row) => (
                <ActionRow
                  key={row.action.workItemId}
                  projectId={project.id}
                  row={row}
                  next={next}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
