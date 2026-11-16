import type { ComponentType } from 'react';
import { postInspectionMetricsTaskOpened } from '@shiplean/core/api/usageApi';
import { LeadTimeChart } from '../chart/LeadTimeChart';
import type { InspectionViewProps } from '../InspectionViewProps';
import { OverviewQualityCard } from '../quality/OverviewQualityCard';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { CoverageBanner } from './CoverageBanner';
import { LeadTimeCard } from './LeadTimeCard';
import { LongestTasks } from './LongestTasks';
import { OverviewEmpty } from './OverviewEmpty';
import { PANEL_HEAD, PANEL_NOTE } from './overviewStyles';

/**
 * The Overview (design ?view=overview&wave=1): the lead-time card for the last finished sprint, the chart and the longest
 * tasks. The shell draws the h1 and the caveat; every click goes to the period view through onQueryChange.
 */
export const OverviewView: ComponentType<InspectionViewProps> = ({
  project,
  view,
  onQueryChange,
}) => {
  const unit = view.cadence === 'weeks' ? 'week' : 'sprint';
  const finished = view.headline.finished;
  const anyTask = view.periods.some((period) => period.leadTime.tasks > 0);

  const openTask = (periodId: string, taskKey: string, from: 'chart' | 'longest') => {
    // The usage call never blocks or fails the page.
    postInspectionMetricsTaskOpened(project.id, from).catch(() => undefined);
    onQueryChange({ view: 'period', period: periodId, select: taskKey });
  };
  const onPeriod = (periodId: string) =>
    onQueryChange({ view: 'period', period: periodId, select: null });
  const onDot = (taskKey: string) => {
    const period = view.periods.find((p) => p.dots.some((dot) => dot.taskKey === taskKey));
    if (period !== undefined) openTask(period.id, taskKey, 'chart');
  };

  const jsonLink = (
    <div className="flex justify-end">
      <button
        type="button"
        data-testid={ids.headerJsonLink}
        className="btn-ghost"
        onClick={() => onQueryChange({ view: 'json' })}
      >
        JSON for agents
      </button>
    </div>
  );

  const chart = (
    <section className="panel">
      <header className="flex flex-wrap items-baseline gap-2 border-b border-edge px-4 py-2.5">
        <h2 className={PANEL_HEAD}>Lead time by {unit}</h2>
        <span className={PANEL_NOTE}>
          from first Active to last commit · click a {unit} for its tasks
        </span>
      </header>
      <div className="p-3">
        <LeadTimeChart periods={view.periods} height={200} onPeriod={onPeriod} onDot={onDot} />
      </div>
    </section>
  );

  if (finished === null || !anyTask) {
    return (
      <section data-testid={ids.overview} className="grid gap-3">
        {jsonLink}
        <OverviewEmpty unit={unit} running={view.headline.soFar?.name ?? null} noTasks={!anyTask} />
        {anyTask ? chart : null}
      </section>
    );
  }

  return (
    <section data-testid={ids.overview} className="grid gap-3">
      {jsonLink}
      <CoverageBanner coverage={view.coverage} unit={unit} />
      <div className="flex flex-wrap gap-3">
        <LeadTimeCard
          headline={view.headline}
          finished={finished}
          periods={view.periods}
          onOpen={() => onQueryChange({ view: 'leadtime' })}
        />
        <OverviewQualityCard view={view} onOpen={() => onQueryChange({ view: 'quality' })} />
      </div>
      {chart}
      <LongestTasks
        tasks={view.longest.slice(0, 6)}
        periods={view.periods}
        unit={unit}
        onTask={(task) => {
          if (task.periodId !== null) openTask(task.periodId, task.key, 'longest');
        }}
      />
    </section>
  );
};
