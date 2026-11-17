import { Link } from 'react-router-dom';
import type { InspectionBugDto, InspectionPeriodDto } from '@shiplean/core/api/views';
import { TypeIcon } from '../../../components/TypeIcon';
import { routePath } from '../../../routes';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { SEVERITY_FILLS, severityPart } from './severity';

const cell = 'px-3 py-2 whitespace-nowrap';
const chip =
  'inline-flex h-5 items-center gap-1 rounded-full px-2 text-[length:var(--text-xs)] font-semibold whitespace-nowrap';

function Severity({ severity }: { severity: number | null }) {
  if (severity === null) return <span className="text-ink-muted">—</span>;
  const part = severityPart(severity);
  return (
    <span
      className="inline-flex h-5 min-w-6 items-center justify-center rounded-[3px] px-1 text-[length:var(--text-xs)] font-bold"
      style={{
        background: SEVERITY_FILLS[part],
        color: severity <= 2 ? 'var(--color-accent-fg)' : 'var(--color-text)',
      }}
    >
      S{severity}
    </span>
  );
}

/**
 * Every bug counted in the shown periods, newest filed first (the server sends oldest first). Caused by links the task;
 * a bug without one offers "+ Caused by", which opens the bug's page with its link picker preset to Caused by.
 */
export function QualityBugs({
  projectKey,
  bugs,
  periods,
}: {
  projectKey: string;
  bugs: readonly InspectionBugDto[];
  periods: readonly InspectionPeriodDto[];
}) {
  const newestFirst = [...bugs].reverse();
  const found = bugs.filter((bug) => bug.foundBeforeRelease).length;
  const shortName = (periodId: string) => periods.find((p) => p.id === periodId)?.shortName;
  return (
    <section className="rounded-md border border-edge bg-panel">
      <header className="flex flex-wrap items-baseline gap-2 border-b border-edge px-3 py-2">
        <h3 className="m-0 text-[length:var(--text-md)] font-semibold">Bugs in the range</h3>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          {bugs.length - found} escaping · {found} found before release
        </span>
      </header>
      {bugs.length === 0 ? (
        <p className="m-0 px-3 py-3 text-ink-muted">No bugs were filed in these sprints.</p>
      ) : (
        <div className="overflow-x-auto">
          <table data-testid={ids.qualityBugRows} className="w-full border-collapse">
            <thead className="text-left text-[length:var(--text-sm)] text-ink-muted">
              <tr className="border-b border-edge">
                <th className={`${cell} font-medium`}>Bug</th>
                <th className={`${cell} font-medium`}>Sev</th>
                <th className={`${cell} font-medium`}>Filed</th>
                <th className={`${cell} font-medium`}>Caused by</th>
                <th className={`${cell} font-medium`}>Counted</th>
              </tr>
            </thead>
            <tbody>
              {newestFirst.map((bug) => (
                <tr
                  key={bug.key}
                  data-testid={ids.qualityBugRow}
                  data-key={bug.key}
                  data-found-before-release={bug.foundBeforeRelease || undefined}
                  className={`border-b border-edge ${bug.foundBeforeRelease ? 'opacity-60' : ''}`}
                >
                  <td className={`${cell} max-w-[420px] overflow-hidden text-ellipsis`}>
                    <Link
                      to={routePath('workItem', { key: projectKey, itemKey: bug.key })}
                      className="inline-flex items-center gap-1.5 text-ink no-underline"
                    >
                      <TypeIcon type="bug" />
                      <span className="font-mono text-[length:var(--text-sm)] text-ink-muted">
                        {bug.key}
                      </span>
                      <span className="overflow-hidden text-ellipsis">{bug.title}</span>
                    </Link>
                  </td>
                  <td className={cell}>
                    <Severity severity={bug.severity} />
                  </td>
                  <td className={`${cell} text-[length:var(--text-sm)]`}>
                    {bug.filedLabel}
                    {shortName(bug.periodId) !== undefined && (
                      <span className="text-ink-muted"> · {shortName(bug.periodId)}</span>
                    )}
                  </td>
                  <td className={cell}>
                    {bug.causedByKey === null ? (
                      <Link
                        data-testid={ids.qualityBugAddCausedBy}
                        to={routePath(
                          'workItem',
                          { key: projectKey, itemKey: bug.key },
                          { addLink: 'causedBy' },
                        )}
                        className="inline-flex h-6 items-center rounded-md border border-dashed border-edge px-2 text-[length:var(--text-xs)] text-ink-muted no-underline hover:text-ink"
                      >
                        + Caused by
                      </Link>
                    ) : (
                      <Link
                        data-testid={ids.qualityBugCausedBy}
                        to={routePath('workItem', { key: projectKey, itemKey: bug.causedByKey })}
                        title={bug.causedByTitle ?? undefined}
                        className={`${chip} bg-sunken text-ink no-underline`}
                      >
                        <TypeIcon type="task" />
                        <span className="font-mono">{bug.causedByKey}</span>
                      </Link>
                    )}
                  </td>
                  <td className={cell}>
                    {bug.foundBeforeRelease ? (
                      <span
                        data-testid={ids.qualityBugFoundBeforeRelease}
                        className={`${chip} bg-sunken`}
                      >
                        Found before release
                      </span>
                    ) : (
                      <span
                        className={chip}
                        style={{
                          background: 'color-mix(in srgb, var(--color-danger) 16%, transparent)',
                        }}
                      >
                        Escaped
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
