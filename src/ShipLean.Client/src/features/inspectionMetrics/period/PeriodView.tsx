import { useEffect, useRef, type ComponentType } from 'react';
import {
  postInspectionMetricsPeriodOpened,
  postInspectionMetricsTaskOpened,
} from '@shiplean/core/api/usageApi';
import type { InspectionViewProps } from '../InspectionViewProps';
import { OVERLINE } from '../overview/overviewStyles';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { PeriodFeatures } from './PeriodFeatures';
import { PeriodHeader } from './PeriodHeader';
import { PeriodRail } from './PeriodRail';
import { PeriodStats } from './PeriodStats';
import { PeriodTasks } from './PeriodTasks';

const tabClass = (active: boolean) =>
  `rounded-control px-3 py-1 text-[length:var(--text-sm)] ${
    active ? 'bg-signal-soft font-semibold text-ink' : 'text-ink-muted hover:text-ink'
  }`;

// ?view=period&p=<sprint id> (design/screens/inspection.html, "One period"): the sprint's numbers, its tasks (slowest first,
// as the server sends them) or its Features, and the rail drawing the selected task's measurement. Another sprint or Back
// is a new history entry; a task or tab replaces it (the page's rule). The page keeps this view mounted while the sprint
// stays the same, so period_opened fires once per sprint.
export const PeriodView: ComponentType<InspectionViewProps> = ({
  project,
  view,
  query,
  onQueryChange,
}) => {
  const detail = view.periodDetail;
  const periodId = detail?.periodId ?? null;
  const inProgress = detail?.inProgress ?? false;
  // Once per sprint id: a refetch that flips inProgress (the sprint ending while open) is not a new open.
  const posted = useRef<string | null>(null);
  useEffect(() => {
    if (periodId === null || posted.current === periodId) return;
    posted.current = periodId;
    // The usage call never blocks or fails the page.
    postInspectionMetricsPeriodOpened(project.id, inProgress ? 'running' : 'finished').catch(
      () => undefined,
    );
  }, [project.id, periodId, inProgress]);

  const unit = view.cadence === 'weeks' ? 'week' : 'sprint';
  const back = () => onQueryChange({ view: 'leadtime', period: null, select: null, tab: 'tasks' });

  if (detail === null) {
    const finished = view.headline.finished;
    return (
      <section data-testid={ids.period} className="grid justify-items-start gap-2 p-6">
        <h2 className="m-0 text-[length:var(--text-lg)] font-semibold">No {unit} chosen</h2>
        <p className="m-0 text-ink-muted">Pick a {unit} on Lead time to see its tasks.</p>
        <div className="flex gap-2">
          <button type="button" data-testid={ids.periodBack} className="btn-ghost" onClick={back}>
            ← Lead time
          </button>
          {finished !== null && (
            <button
              type="button"
              className="btn-ghost"
              onClick={() => onQueryChange({ period: finished.periodId, select: null })}
            >
              Open {finished.name}
            </button>
          )}
        </div>
      </section>
    );
  }

  const tab = query.tab;
  return (
    <section data-testid={ids.period} data-period-id={detail.periodId} className="grid gap-3">
      <PeriodHeader
        detail={detail}
        onBack={back}
        onPeriod={(id) => onQueryChange({ period: id, select: null })}
      />
      <PeriodStats stats={detail.leadTime} unit={unit} />
      <nav data-testid={ids.periodTabs} aria-label={`The ${unit}`} className="flex gap-1">
        <button
          type="button"
          data-testid={ids.periodTabTasks}
          aria-current={tab === 'tasks' ? 'page' : undefined}
          className={tabClass(tab === 'tasks')}
          onClick={() => onQueryChange({ tab: 'tasks' })}
        >
          Tasks {detail.tasks.length}
        </button>
        <button
          type="button"
          data-testid={ids.periodTabFeatures}
          aria-current={tab === 'features' ? 'page' : undefined}
          className={tabClass(tab === 'features')}
          onClick={() => onQueryChange({ tab: 'features' })}
        >
          Features {detail.features.length}
        </button>
      </nav>
      <div className="grid grid-cols-[minmax(0,1fr)_340px] items-start gap-3">
        <div className="panel overflow-hidden">
          {tab === 'tasks' ? (
            <PeriodTasks
              tasks={detail.tasks}
              selected={detail.selected?.task.key ?? null}
              inProgress={detail.inProgress}
              onSelect={(key) => onQueryChange({ select: key })}
            />
          ) : (
            <PeriodFeatures
              features={detail.features}
              projectKey={project.key}
              unit={unit}
              inProgress={detail.inProgress}
            />
          )}
        </div>
        {tab === 'tasks' ? (
          <PeriodRail
            projectKey={project.key}
            selected={detail.selected}
            noTasks={detail.tasks.length === 0}
            onOpenTask={() =>
              postInspectionMetricsTaskOpened(project.id, 'rail').catch(() => undefined)
            }
          />
        ) : (
          <aside className="panel grid gap-1 p-4">
            <div className={OVERLINE}>This {unit}</div>
            <p className="m-0 text-[length:var(--text-sm)]">
              {detail.features.length} Features had tasks end. Open a Feature&apos;s timeline to see
              where its time went: blocked bars, review rounds and what waited on what.
            </p>
          </aside>
        )}
      </div>
    </section>
  );
};
