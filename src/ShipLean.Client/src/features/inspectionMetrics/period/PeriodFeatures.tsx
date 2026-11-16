import { Link } from 'react-router-dom';
import type { InspectionPeriodFeatureDto } from '@shiplean/core/api/views';
import { TypeIcon } from '../../../components/TypeIcon';
import { routePath } from '../../../routes';
import { SPLIT_COLOURS } from '../leadtime/SplitBar';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { PartsBar } from './PartsBar';
import { noTasksText } from './PeriodTasks';

const cell = 'px-3 py-2';

/** How much of each Feature ended in the sprint: ended before (pale) and during it, out of all its tasks. */
function EndedBar({ feature }: { feature: InspectionPeriodFeatureDto }) {
  const total = Math.max(feature.tasksTotal, 1);
  return (
    <span className="inline-flex h-2 w-[120px] overflow-hidden rounded-sm bg-sunken align-middle">
      <i
        className="block h-full opacity-35"
        style={{
          width: `${(feature.tasksEndedBefore / total) * 100}%`,
          background: SPLIT_COLOURS.active,
        }}
      />
      <i
        className="block h-full"
        style={{
          width: `${(feature.tasksEnded / total) * 100}%`,
          background: SPLIT_COLOURS.active,
        }}
      />
    </span>
  );
}

/** The Features tab: each Feature with a task that ended in the sprint, and a link to its timeline. */
export function PeriodFeatures({
  features,
  projectKey,
  unit,
  inProgress,
}: {
  features: readonly InspectionPeriodFeatureDto[];
  projectKey: string;
  unit: string;
  inProgress: boolean;
}) {
  const max = Math.max(0, ...features.map((f) => f.leadTime.meanHours ?? 0));
  return (
    <>
      <table
        data-testid={ids.featureRows}
        className="w-full border-collapse text-[length:var(--text-sm)]"
      >
        <thead className="text-left text-ink-muted">
          <tr className="border-b border-edge">
            <th className={`${cell} font-medium`}>Feature</th>
            <th className={`${cell} font-medium`}>Tasks ended this {unit}</th>
            <th className={`${cell} w-[22%] font-medium`}>Mean lead time</th>
            <th className={`${cell} font-medium`}>Longest task</th>
            <th className={`${cell} text-right font-medium`}>Execution so far</th>
            <th className={cell} />
          </tr>
        </thead>
        <tbody>
          {features.length === 0 && (
            <tr>
              <td colSpan={6} className={`${cell} text-ink-muted`}>
                {noTasksText(inProgress)}
              </td>
            </tr>
          )}
          {features.map((feature) => {
            const stats = feature.leadTime;
            return (
              <tr
                key={feature.key}
                data-testid={ids.featureRow}
                data-key={feature.key}
                className="border-b border-edge"
              >
                <td className={cell}>
                  <span className="inline-flex items-center gap-1.5">
                    <TypeIcon type="feature" />
                    {feature.title}
                    <span className="font-mono text-[length:var(--text-xs)] text-ink-muted">
                      {feature.key}
                    </span>
                  </span>
                </td>
                <td className={cell}>
                  <span className="mr-2">
                    <b>{feature.tasksEnded}</b> of {feature.tasksTotal}{' '}
                    <span className="text-ink-muted">· {feature.tasksEndedBefore} before</span>
                  </span>
                  <EndedBar feature={feature} />
                </td>
                <td className={cell}>
                  <div className="grid grid-cols-[64px_minmax(60px,1fr)] items-center gap-2">
                    <b className="text-right tabular-nums">{stats.meanLabel ?? '—'}</b>
                    <PartsBar
                      lead={stats.meanHours ?? 0}
                      parts={{
                        active: stats.meanActiveHours ?? 0,
                        blocked: stats.meanBlockedHours ?? 0,
                        rework: stats.meanReworkHours ?? 0,
                      }}
                      max={max}
                    />
                  </div>
                </td>
                <td className={cell}>
                  {feature.longestTaskKey === null ? (
                    '—'
                  ) : (
                    <>
                      <span className="font-mono text-[length:var(--text-xs)] text-ink-muted">
                        {feature.longestTaskKey}
                      </span>{' '}
                      {feature.longestLeadLabel}
                    </>
                  )}
                </td>
                <td className={`${cell} text-right tabular-nums`}>
                  {feature.executionLabel ?? '—'}
                </td>
                <td className={cell}>
                  <Link
                    className="btn-ghost no-underline"
                    to={routePath('featureTimeline', { key: projectKey, itemKey: feature.key })}
                  >
                    Timeline
                  </Link>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <p className="m-0 px-3 py-2 text-[length:var(--text-sm)] text-ink-muted">
        Features don&apos;t belong to sprints. A Feature shows here when any of its tasks ended in
        this {unit}; the bar is how much of it had ended before (pale) and during this {unit}.
      </p>
    </>
  );
}
