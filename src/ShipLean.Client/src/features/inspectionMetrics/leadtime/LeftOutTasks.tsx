import type { InspectionLeftOutDto } from '@shiplean/core/api/views';
import { inspectionMetricsTestIds as ids } from '../testIds';

// A project can close hundreds of tasks with no start, so past this many the list starts folded.
const OPEN_UP_TO = 5;

const cell = 'px-3 py-1.5';

/** The tasks with no lead time and why, one row each; folded behind its count when the list is long. */
export function LeftOutTasks({ tasks }: { tasks: readonly InspectionLeftOutDto[] }) {
  return (
    <details
      data-testid={ids.leftOut}
      open={tasks.length <= OPEN_UP_TO}
      className="rounded-md border border-edge bg-panel [&[open]>summary]:border-b [&[open]>summary]:border-edge"
    >
      <summary className="flex cursor-pointer flex-wrap items-baseline gap-2 px-3 py-2">
        <h3 className="m-0 text-[length:var(--text-md)] font-semibold">
          Left out: {tasks.length} {tasks.length === 1 ? 'task' : 'tasks'}
        </h3>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          a task with no start has no lead time; the timeline shows it as a diamond
        </span>
      </summary>
      <div className="max-h-80 overflow-y-auto">
        <table className="w-full table-fixed border-collapse text-[length:var(--text-sm)]">
          <tbody>
            {tasks.map((task) => (
              <tr
                key={task.key}
                data-testid={ids.leftOutRow}
                data-key={task.key}
                className="border-b border-edge last:border-b-0"
              >
                <td className={`${cell} w-24 font-mono whitespace-nowrap`}>{task.key}</td>
                <td className={`${cell} truncate`} title={task.title}>
                  {task.title}
                </td>
                <td className={`${cell} w-[30%] truncate text-ink-muted`} title={task.reason}>
                  {task.reason}
                </td>
                <td className={`${cell} w-40 text-right whitespace-nowrap text-ink-muted`}>
                  {task.atLabel}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </details>
  );
}
