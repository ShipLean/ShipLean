import { TypeIcon } from '../../../components/TypeIcon';
import { useProjectSprints } from '../../workItem/workItemApi';
import { retrosTestIds as ids } from '../testIds';
import { AddActionItemButton } from './actions/AddActionItemButton';
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
  const recurring = new Set(
    insights.themes.filter((t) => t.status === 'recurring').map((t) => t.theme.id),
  );
  return (
    <div className="flex flex-col gap-3">
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        <TypeIcon type="action" /> <b>Action item</b> is a work item type at the same level as
        stories and bugs. It sits in the Stories backlog, carries points and is pulled into a sprint
        like any story; it also links to the theme it addresses.
      </p>
      <section className="rounded-frame border border-edge bg-panel">
        <header className="flex items-baseline gap-3 px-4 py-3">
          <h2 className="m-0 text-[length:var(--text-md)] font-semibold">Action items</h2>
          <span className="text-[length:var(--text-sm)] text-ink-muted">
            {actionItems.length - done} open · {done} done
          </span>
          <span className="flex-1" />
          <AddActionItemButton />
        </header>
        {actionItems.length === 0 ? (
          <p className="m-0 px-4 pb-4 text-[length:var(--text-sm)] text-ink-muted">Nothing here.</p>
        ) : (
          <div className="overflow-x-auto">
            <table data-testid={ids.retroActionsTable} className="w-full border-collapse">
              <thead>
                <tr>
                  <th className={HEAD}>Action</th>
                  <th className={HEAD}>Fix</th>
                  <th className={`${HEAD} text-right`}>Covers</th>
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
                    mode={insights.mode}
                    recurring={row.theme ? recurring.has(row.theme.id) : false}
                  />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
