import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { sprintsTestIds as ids } from '../testIds';
import type { StateChanger } from './RowStateMenu';
import { TaskRow, TouchTaskRow } from './TaskRow';

/** The sprint's tasks that have no story, under a heading row, below every story. */
export function UnparentedTasks({
  tasks,
  changer,
}: {
  tasks: WorkItemDto[];
  changer: StateChanger;
}) {
  if (tasks.length === 0) return null;
  return (
    <>
      <tr data-testid={ids.unparentedHeading} className="border-b border-edge bg-sunken">
        <th
          colSpan={6}
          className="px-3 py-1.5 text-left text-[length:var(--text-xs)] font-semibold text-ink-muted"
        >
          Tasks without a story
        </th>
      </tr>
      {tasks.map((task) => (
        <TaskRow key={task.id} task={task} changer={changer} />
      ))}
    </>
  );
}

/** The same section at touch: a heading, then the tasks as two-line rows. */
export function TouchUnparentedTasks({
  tasks,
  changer,
}: {
  tasks: WorkItemDto[];
  changer: StateChanger;
}) {
  if (tasks.length === 0) return null;
  return (
    <>
      <li
        data-testid={ids.unparentedHeading}
        className="border-b border-edge bg-sunken px-3 py-1.5 text-[length:var(--text-xs)] font-semibold text-ink-muted"
      >
        Tasks without a story
      </li>
      {tasks.map((task) => (
        <TouchTaskRow key={task.id} task={task} changer={changer} />
      ))}
    </>
  );
}
