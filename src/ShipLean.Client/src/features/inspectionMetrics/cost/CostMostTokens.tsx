import { Link } from 'react-router-dom';
import type { InspectionCostFeatureDto, InspectionCostTaskDto } from '@shiplean/core/api/views';
import { TypeIcon } from '../../../components/TypeIcon';
import { routePath } from '../../../routes';
import { inspectionMetricsTestIds as ids } from '../testIds';

const cell = 'px-3 py-2';
const num = `${cell} text-right tabular-nums whitespace-nowrap`;
const PANEL = 'rounded-md border border-edge bg-panel';
const HEAD = 'flex flex-wrap items-baseline gap-2 border-b border-edge px-3 py-2';
const H3 = 'm-0 text-[length:var(--text-md)] font-semibold';
const NOTE = 'text-[length:var(--text-sm)] text-ink-muted';

/**
 * The range's tasks with the most tokens, most first (the server's top 8). A task opens its item page, where its Agent runs
 * section lists the runs to read.
 */
export function CostMostTokens({
  projectKey,
  tasks,
  onOpen,
}: {
  projectKey: string;
  tasks: readonly InspectionCostTaskDto[];
  onOpen(task: InspectionCostTaskDto): void;
}) {
  return (
    <section className={PANEL}>
      <header className={HEAD}>
        <h3 className={H3}>Most tokens</h3>
        <span className={NOTE}>the tasks to read the runs of</span>
      </header>
      <div className="overflow-x-auto">
        <table
          data-testid={ids.costMostTokensRows}
          className="w-full border-collapse text-[length:var(--text-sm)]"
        >
          <thead className="text-left text-ink-muted">
            <tr className="border-b border-edge">
              <th className={`${cell} font-medium`}>Task</th>
              <th className={`${cell} font-medium`}>Who</th>
              <th className={`${cell} font-medium`}>Model</th>
              <th className={`${num} font-medium`}>Runs</th>
              <th className={`${num} font-medium`}>Tokens</th>
              <th className={`${num} font-medium`}>Lead time</th>
            </tr>
          </thead>
          <tbody>
            {tasks.map((task) => (
              <tr
                key={task.key}
                data-testid={ids.costMostTokensRow}
                data-key={task.key}
                className="border-b border-edge"
              >
                <td className={cell}>
                  <Link
                    data-testid={ids.costMostTokensLink}
                    to={routePath('workItem', { key: projectKey, itemKey: task.key })}
                    onClick={() => onOpen(task)}
                    className="inline-flex items-center gap-1.5 text-ink no-underline hover:underline"
                  >
                    <TypeIcon type="task" />
                    <span className="font-mono text-[length:var(--text-xs)] text-ink-muted">
                      {task.key}
                    </span>
                    <span>{task.title}</span>
                  </Link>
                </td>
                <td className={`${cell} whitespace-nowrap`}>
                  {task.who}
                  {task.agent && <span className="text-ink-muted"> · agent</span>}
                </td>
                <td className={cell}>
                  {task.models.length === 0 ? (
                    <span className="text-ink-muted">—</span>
                  ) : (
                    <span className="flex flex-wrap gap-1">
                      {task.models.map((model) => (
                        <span
                          key={model}
                          className="rounded-full bg-sunken px-2 py-0.5 font-mono text-[length:var(--text-xs)] whitespace-nowrap"
                        >
                          {model}
                        </span>
                      ))}
                    </span>
                  )}
                </td>
                <td className={num}>{task.runs}</td>
                <td className={`${num} font-semibold`}>{task.tokensLabel}</td>
                <td className={num}>{task.leadLabel}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

/** The range's tokens per Feature, most first, each with a bar against the largest. */
export function CostByFeature({ features }: { features: readonly InspectionCostFeatureDto[] }) {
  const max = Math.max(1, ...features.map((f) => f.tokens));
  return (
    <section className={PANEL}>
      <header className={HEAD}>
        <h3 className={H3}>By Feature</h3>
        <span className={NOTE}>tokens in the range</span>
      </header>
      <table
        data-testid={ids.costFeatureRows}
        className="w-full border-collapse text-[length:var(--text-sm)]"
      >
        <tbody>
          {features.map((feature) => (
            <tr
              key={feature.key ?? 'none'}
              data-testid={ids.costFeatureRow}
              data-key={feature.key ?? undefined}
              className="border-b border-edge"
            >
              <td className={cell}>
                {feature.key !== null && (
                  <span className="font-mono text-[length:var(--text-xs)] text-ink-muted">
                    {feature.key}{' '}
                  </span>
                )}
                {feature.title}
              </td>
              <td className={`${num} text-ink-muted`}>
                {feature.tasks} {feature.tasks === 1 ? 'task' : 'tasks'}
              </td>
              <td className={`${cell} w-[40%]`}>
                <div className="flex items-center gap-2">
                  <b className="w-12 tabular-nums">{feature.tokensLabel}</b>
                  <span
                    aria-hidden="true"
                    className="block h-2 rounded-[2px]"
                    style={{
                      width: `${(feature.tokens / max) * 100}%`,
                      background: 'var(--type-feature)',
                    }}
                  />
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
