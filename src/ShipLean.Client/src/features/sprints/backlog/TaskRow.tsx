import type { WorkItemDto } from '../../../api/workItems';
import { Avatar } from '../../../components/Avatar';
import { TypeIcon } from '../../../components/TypeIcon';
import { ItemLink } from '../../itemPeek/ItemLink';
import { sprintsTestIds as ids } from '../testIds';
import { RowStateMenu, type StateChanger } from './RowStateMenu';

/** One task, indented under its story (or listed on its own). Task points are informational: sprint totals count stories and bugs. */
export function TaskRow({ task, changer }: { task: WorkItemDto; changer: StateChanger }) {
  return (
    <tr
      data-testid={ids.taskRow}
      data-key={task.key}
      className="border-b border-edge bg-sunken/40 text-[length:var(--text-sm)]"
    >
      <td className="py-1.5 pr-3 pl-9">
        <span className="inline-flex items-center gap-2">
          <TypeIcon type={task.type.id} />
          <ItemLink itemKey={task.key} className="text-inherit no-underline hover:underline">
            {task.title}
          </ItemLink>
        </span>
      </td>
      <td className="px-3 py-1.5 whitespace-nowrap text-ink-muted">{task.key}</td>
      <td className="px-3 py-1.5">
        <RowStateMenu item={task} changer={changer} />
      </td>
      <td className="px-3 py-1.5">{task.assignee ? <Avatar {...task.assignee} /> : null}</td>
      <td className="px-3 py-1.5" />
      <td
        className="px-3 py-1.5 text-right text-ink-muted tabular-nums"
        title="Task points are informational; sprint totals count stories and bugs"
      >
        {task.points ?? '—'}
      </td>
    </tr>
  );
}
