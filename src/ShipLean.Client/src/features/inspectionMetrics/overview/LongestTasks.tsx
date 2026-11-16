import type { InspectionPeriodDto, InspectionTaskDto } from '@shiplean/core/api/views';
import { TypeIcon } from '../../../components/TypeIcon';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { PANEL_HEAD, PANEL_NOTE } from './overviewStyles';

const PARTS = [
  {
    name: 'Active',
    hours: (t: InspectionTaskDto) => t.activeHours,
    colour: 'var(--chart-completed)',
  },
  {
    name: 'Blocked',
    hours: (t: InspectionTaskDto) => t.blockedHours,
    colour: 'var(--color-danger)',
  },
  { name: 'Rework', hours: (t: InspectionTaskDto) => t.reworkHours, colour: 'var(--color-warn)' },
] as const;

function TaskLabel({ task }: { task: InspectionTaskDto }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      <TypeIcon type="task" />
      <span className="font-mono text-[length:var(--text-xs)] text-ink-muted">{task.key}</span>
      <span>{task.title}</span>
    </span>
  );
}

/** The range's longest tasks, each with its lead time split into active, blocked and rework; a row opens its sprint. */
export function LongestTasks({
  tasks,
  periods,
  unit,
  onTask,
}: {
  tasks: readonly InspectionTaskDto[];
  periods: readonly InspectionPeriodDto[];
  unit: 'sprint' | 'week';
  onTask(task: InspectionTaskDto): void;
}) {
  const max = Math.max(...tasks.map((t) => t.leadHours), 1);
  const first = periods.at(0);
  const last = periods.at(-1);
  return (
    <section data-testid={ids.overviewLongest} className="panel overflow-hidden">
      <header className="flex flex-wrap items-baseline gap-2 border-b border-edge px-4 py-2.5">
        <h2 className={PANEL_HEAD}>Longest tasks</h2>
        {first === undefined || last === undefined ? null : (
          <span className={PANEL_NOTE}>
            {first.name} – {last.name}
          </span>
        )}
        <span className="flex-1" />
        {PARTS.map((part) => (
          <span key={part.name} className={`inline-flex items-center gap-1 ${PANEL_NOTE}`}>
            <i
              aria-hidden="true"
              className="size-2.5 rounded-[2px]"
              style={{ background: part.colour }}
            />
            {part.name}
          </span>
        ))}
      </header>
      <table className="w-full border-collapse text-[length:var(--text-sm)]">
        <thead className="text-left text-ink-muted">
          <tr>
            <th className="px-3 py-2 font-medium">Task</th>
            <th className="px-3 py-2 font-medium">Feature</th>
            <th className="px-3 py-2 font-medium">Who</th>
            <th className="px-3 py-2 font-medium">{unit === 'week' ? 'Week' : 'Sprint'}</th>
            <th className="w-[34%] px-3 py-2 font-medium">Lead time</th>
          </tr>
        </thead>
        <tbody>
          {tasks.map((task) => (
            <tr
              key={task.key}
              data-testid={ids.overviewLongestRow}
              data-key={task.key}
              className={`border-t border-edge ${task.periodId === null ? '' : 'cursor-pointer hover:bg-sunken'}`}
              onClick={task.periodId === null ? undefined : () => onTask(task)}
            >
              <td className="px-3 py-2">
                {task.periodId === null ? (
                  <TaskLabel task={task} />
                ) : (
                  <button
                    type="button"
                    className="inline-flex items-center gap-1.5 text-left"
                    onClick={(event) => {
                      event.stopPropagation();
                      onTask(task);
                    }}
                  >
                    <TaskLabel task={task} />
                  </button>
                )}
              </td>
              <td className="px-3 py-2">{task.featureTitle ?? '—'}</td>
              <td className="px-3 py-2">{task.who}</td>
              <td className="px-3 py-2">
                {periods.find((p) => p.id === task.periodId)?.shortName ?? '—'}
              </td>
              <td className="px-3 py-2">
                <div className="flex items-center gap-2">
                  <b className="w-16 flex-none text-right">{task.leadLabel}</b>
                  <span
                    className="flex h-2 overflow-hidden rounded-full"
                    style={{ width: `${Math.max(2, (task.leadHours / max) * 100)}%` }}
                  >
                    {PARTS.map((part) => (
                      <i
                        key={part.name}
                        style={{
                          background: part.colour,
                          width: `${task.leadHours > 0 ? (part.hours(task) / task.leadHours) * 100 : 0}%`,
                        }}
                      />
                    ))}
                  </span>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
