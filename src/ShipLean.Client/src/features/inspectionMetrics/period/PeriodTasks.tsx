import type { InspectionTaskDto } from '@shiplean/core/api/views';
import { TypeIcon } from '../../../components/TypeIcon';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { PartsBar } from './PartsBar';

const cell = 'px-3 py-2';
const num = `${cell} text-right tabular-nums`;

export const noTasksText = (inProgress: boolean) =>
  inProgress ? 'No tasks ended in this sprint yet' : 'No tasks ended in this sprint';

/** Every task that ended in the sprint, in the server's order (slowest first); a row selects its task for the rail. */
export function PeriodTasks({
  tasks,
  selected,
  inProgress,
  onSelect,
}: {
  tasks: readonly InspectionTaskDto[];
  selected: string | null;
  /** The sprint is still running, so "yet" when nothing has ended. */
  inProgress: boolean;
  onSelect(taskKey: string): void;
}) {
  const max = Math.max(0, ...tasks.map((t) => t.leadHours));
  return (
    <table
      data-testid={ids.taskRows}
      className="w-full border-collapse text-[length:var(--text-sm)]"
    >
      <thead className="text-left text-ink-muted">
        <tr className="border-b border-edge">
          <th className={`${cell} font-medium`}>Task</th>
          <th className={`${cell} font-medium`}>Feature</th>
          <th className={`${cell} font-medium`}>Who</th>
          <th className={`${cell} w-[26%] font-medium`}>Lead time</th>
          <th className={`${num} font-medium`} title="Review rounds">
            Rev.
          </th>
          <th className={`${num} font-medium`}>Commits</th>
        </tr>
      </thead>
      <tbody>
        {tasks.length === 0 && (
          <tr>
            <td colSpan={6} className={`${cell} text-ink-muted`}>
              {noTasksText(inProgress)}
            </td>
          </tr>
        )}
        {tasks.map((task) => (
          <tr
            key={task.key}
            data-testid={ids.taskRow}
            data-key={task.key}
            aria-selected={task.key === selected}
            className={`cursor-pointer border-b border-edge hover:bg-sunken ${
              task.key === selected ? 'bg-signal-soft' : ''
            }`}
            onClick={() => onSelect(task.key)}
          >
            <td className={cell}>
              <button
                type="button"
                className="inline-flex items-center gap-1.5 text-left"
                onClick={(event) => {
                  event.stopPropagation();
                  onSelect(task.key);
                }}
              >
                <TypeIcon type="task" />
                <span className="font-mono text-[length:var(--text-xs)] text-ink-muted">
                  {task.key}
                </span>
                <span>{task.title}</span>
              </button>
              {task.parentKey !== null && (
                <span className="ml-1 text-[length:var(--text-xs)] text-ink-muted">
                  under <span className="font-mono">{task.parentKey}</span>
                </span>
              )}
            </td>
            <td className={`${cell} whitespace-nowrap`}>{task.featureTitle ?? '—'}</td>
            <td className={`${cell} whitespace-nowrap`}>
              {task.who}
              {task.agent && <span className="text-ink-muted"> (agent)</span>}
            </td>
            <td className={cell}>
              <div className="grid grid-cols-[64px_minmax(60px,1fr)] items-center gap-2">
                <b className="text-right tabular-nums">{task.leadLabel}</b>
                <span className="flex items-center gap-1.5">
                  <PartsBar
                    lead={task.leadHours}
                    parts={{
                      active: task.activeHours,
                      blocked: task.blockedHours,
                      rework: task.reworkHours,
                    }}
                    max={max}
                  />
                  {task.endedBy === 'resolved' && (
                    <span
                      data-testid={ids.taskRowEndedBy}
                      className="text-[length:var(--text-xs)] font-semibold whitespace-nowrap text-caution"
                      title="No commit time was reported, so the lead time ends at Resolved"
                    >
                      to Resolved
                    </span>
                  )}
                </span>
              </div>
            </td>
            <td data-testid={ids.taskRowReviews} className={num}>
              {task.reviews}
            </td>
            <td data-testid={ids.taskRowCommits} className={num}>
              {task.commits}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
