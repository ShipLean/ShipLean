import type { ChildSummaryDto } from '../../../api/workItems';
import { Avatar } from '../../../components/Avatar';
import { TypeIcon } from '../../../components/TypeIcon';
import { ItemLink } from '../../itemPeek/ItemLink';
import { selectClass } from '../form/inputClasses';
import { workItemTestIds as ids } from '../testIds';
import { useProjectStates } from '../workItemApi';
import { useTaskState } from './useTaskState';

// One child task: its title opens it, its state is a select that saves at once (the task is its own work item), and the
// assignee is an avatar.
export function TaskRow({
  task,
  project,
  parentKey,
}: {
  task: ChildSummaryDto;
  project: { id: string; key: string };
  parentKey: string;
}) {
  const states = useProjectStates(project.id);
  const move = useTaskState(project.id, parentKey);
  const options = (states.data ?? [])
    .filter((state) => state.type === 'task')
    .sort((a, b) => a.order - b.order);
  return (
    <li
      data-testid={ids.taskRow}
      data-key={task.key}
      className="flex min-w-0 items-center gap-2 border-t border-edge px-2 py-1.5 text-[length:var(--text-sm)] first:border-t-0"
    >
      <TypeIcon type="task" />
      <ItemLink
        testId={ids.taskOpen}
        projectKey={project.key}
        itemKey={task.key}
        title={task.title}
        className="min-w-0 flex-1 truncate hover:text-signal hover:underline"
      >
        {task.title}
      </ItemLink>
      <span data-testid={ids.taskKey} className="w-20 shrink-0 text-ink-muted">
        {task.key}
      </span>
      <select
        data-testid={ids.taskState}
        aria-label={`State of ${task.key}`}
        className={`${selectClass} w-32 shrink-0`}
        value={task.state.id}
        disabled={move.isPending}
        onChange={(event) => move.mutate({ task, stateId: event.target.value })}
      >
        {options.some((state) => state.id === task.state.id) ? null : (
          <option value={task.state.id}>{task.state.name}</option>
        )}
        {options.map((state) => (
          <option key={state.id} value={state.id}>
            {state.name}
          </option>
        ))}
      </select>
      <span className="w-6 shrink-0">
        <Avatar {...(task.assignee ?? {})} />
      </span>
      {move.error ? (
        <span
          data-testid={ids.taskStateError}
          role="alert"
          className="text-[length:var(--text-xs)] text-alert"
        >
          {move.error.message}
        </span>
      ) : null}
    </li>
  );
}
