import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { Avatar } from '../../../components/Avatar';
import { TypeIcon } from '../../../components/TypeIcon';
import { ItemLink } from '../../itemPeek/ItemLink';
import { sprintsTestIds as ids } from '../testIds';
import { RowStateMenu, type StateChanger } from './RowStateMenu';
import { StatesSheet } from './StatesSheet';

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

// The row whose States sheet is open takes the accent tint (design .row.hit).
const OPEN_ROW = 'has-[[aria-haspopup=dialog][aria-expanded=true]]:bg-signal-soft';

/** A task as two lines at touch, indented under its story: title, then key and state pill, the assignee on the right. No points. */
export function TouchTaskRow({ task, changer }: { task: WorkItemDto; changer: StateChanger }) {
  const flash = changer.flashing === task.key;
  return (
    <li
      data-testid={ids.taskRow}
      data-key={task.key}
      data-flash={flash}
      className={`grid min-h-[50px] grid-cols-[32px_minmax(0,1fr)_40px] items-start gap-x-1 border-b border-edge py-1.5 pr-2 pl-1 transition-colors duration-700 ${OPEN_ROW} ${flash ? 'bg-caution/15' : 'bg-sunken/40'}`}
    >
      <span />
      <div className="grid min-w-0 gap-1 pl-3.5">
        <div className="flex items-start gap-2 text-[length:var(--text-base)] leading-snug">
          <span className="mt-0.5 flex-none">
            <TypeIcon type={task.type.id} />
          </span>
          <ItemLink
            itemKey={task.key}
            className="line-clamp-2 min-h-[var(--touch-target)] min-w-0 text-inherit no-underline"
          >
            {task.title}
          </ItemLink>
        </div>
        <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 pl-6 text-[length:var(--text-xs)] text-ink-muted">
          <span className="font-mono">{task.key}</span>
          <StatesSheet item={task} changer={changer} />
        </div>
      </div>
      <div className="grid justify-items-end">
        {task.assignee ? <Avatar {...task.assignee} /> : null}
      </div>
    </li>
  );
}
