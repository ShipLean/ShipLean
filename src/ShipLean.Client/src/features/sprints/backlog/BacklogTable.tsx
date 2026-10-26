import { useState } from 'react';
import type { WorkItemDto } from '../../../api/workItems';
import { conflictOf } from '../../../api/workItems';
import { useProjectStates } from '../../workItem/workItemApi';
import { useChangeItemState } from '../sprintsApi';
import { sprintsTestIds as ids } from '../testIds';
import type { StateChanger } from './RowStateMenu';
import { StoryWithTasks } from './StoryWithTasks';
import type { SprintStory } from './sprintStories';
import { UnparentedTasks } from './UnparentedTasks';

const HEADINGS = ['Title', 'Key', 'State', 'Assigned', 'Tasks'] as const;

interface BacklogTableProps {
  projectId: string;
  sprintId: string;
  stories: SprintStory[];
  unparented: WorkItemDto[];
}

/** The sprint's stories and bugs, each with its tasks nested under it, then the tasks with no story. A row's state changes in place. */
export function BacklogTable({ projectId, sprintId, stories, unparented }: BacklogTableProps) {
  const [showTasks, setShowTasks] = useState(true);
  const [closed, setClosed] = useState<ReadonlySet<string>>(new Set());
  const [notice, setNotice] = useState<string | null>(null);
  const states = useProjectStates(projectId);
  const change = useChangeItemState(projectId, sprintId);
  const changer: StateChanger = {
    states: states.data ?? [],
    busy: change.isPending,
    onChange: (item, stateId) => {
      setNotice(null);
      change.mutate(
        { item, stateId },
        {
          onError: (error) =>
            setNotice(
              conflictOf(error)
                ? `${item.key} was changed by someone else, so its row is refreshed. Pick its state again.`
                : error.message,
            ),
        },
      );
    },
  };
  const toggle = (id: string) =>
    setClosed((current) => {
      const next = new Set(current);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  const empty = stories.length === 0 && unparented.length === 0;
  return (
    <section className="grid gap-2">
      <div className="flex items-center justify-between gap-3">
        <p
          data-testid={ids.stateNotice}
          role="status"
          hidden={notice === null}
          className="m-0 text-[length:var(--text-sm)] text-caution"
        >
          {notice}
        </p>
        <label className="ml-auto flex items-center gap-1.5 text-[length:var(--text-sm)]">
          <input
            type="checkbox"
            data-testid={ids.showTasks}
            checked={showTasks}
            onChange={(event) => setShowTasks(event.target.checked)}
          />
          Show tasks
        </label>
      </div>
      <div className="overflow-x-auto rounded-md border border-edge bg-panel">
        <table data-testid={ids.table} className="w-full border-collapse text-left">
          <thead>
            <tr className="bg-sunken text-[length:var(--text-xs)] text-ink-muted">
              {HEADINGS.map((heading) => (
                <th key={heading} className="px-3 py-2 font-semibold">
                  {heading}
                </th>
              ))}
              <th className="px-3 py-2 text-right font-semibold">Pts</th>
            </tr>
          </thead>
          <tbody>
            {empty ? (
              <tr data-testid={ids.tableEmpty}>
                <td colSpan={6} className="px-3 py-3 text-ink-muted">
                  No stories or bugs in this sprint.
                </td>
              </tr>
            ) : null}
            {stories.map((entry) => (
              <StoryWithTasks
                key={entry.story.id}
                entry={entry}
                changer={changer}
                open={showTasks && !closed.has(entry.story.id)}
                onToggle={() => toggle(entry.story.id)}
              />
            ))}
            {showTasks ? <UnparentedTasks tasks={unparented} changer={changer} /> : null}
          </tbody>
        </table>
      </div>
    </section>
  );
}
