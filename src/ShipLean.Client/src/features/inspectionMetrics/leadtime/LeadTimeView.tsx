import type { ComponentType } from 'react';
import { postInspectionMetricsTaskOpened } from '@shiplean/core/api/usageApi';
import { LeadTimeChart } from '../chart/LeadTimeChart';
import type { InspectionViewProps } from '../InspectionViewProps';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { LeadTimeCutTable } from './LeadTimeCutTable';
import { LeadTimeRows } from './LeadTimeRows';
import { LeadTimeRule } from './LeadTimeRule';
import { LeadTimeToolbar } from './LeadTimeToolbar';

// ?view=leadtime (design/screens/inspection.html?view=leadtime): how a task's lead time is measured, the chart, then a row
// per sprint with quarter roll-ups, or the split by Feature or by who. Range and split replace the address; a sprint or a
// dot opens the period view (a new history entry, the page's rule).
export const LeadTimeView: ComponentType<InspectionViewProps> = ({
  project,
  view,
  query,
  onQueryChange,
}) => {
  const unit = view.cadence === 'weeks' ? 'week' : 'sprint';
  const openPeriod = (periodId: string) =>
    onQueryChange({ view: 'period', period: periodId, select: null, tab: 'tasks' });
  const openTask = (taskKey: string) => {
    const period = view.periods.find((p) => p.dots.some((dot) => dot.taskKey === taskKey));
    if (period === undefined) return;
    postInspectionMetricsTaskOpened(project.id, 'chart').catch(() => undefined);
    onQueryChange({ view: 'period', period: period.id, select: taskKey, tab: 'tasks' });
  };

  return (
    <section data-testid={ids.leadTime} className="grid gap-3">
      <div className="flex flex-wrap items-baseline gap-2">
        <h2 className="m-0 text-[length:var(--text-lg)] font-semibold">Lead time</h2>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          per task, from its first move to Active (or pick_up) to its last commit · mean per {unit}
        </span>
      </div>
      <LeadTimeToolbar
        range={query.range}
        cut={query.cut}
        coverage={view.coverage}
        unit={unit}
        onRange={(range) => onQueryChange({ range })}
        onCut={(cut) => onQueryChange({ cut })}
      />
      <LeadTimeRule />
      <section className="rounded-md border border-edge bg-panel">
        <header className="flex flex-wrap items-baseline gap-2 border-b border-edge px-3 py-2">
          <h3 className="m-0 text-[length:var(--text-md)] font-semibold">
            Mean lead time and every task
          </h3>
          <span className="text-[length:var(--text-sm)] text-ink-muted">
            click a {unit} to see its tasks
          </span>
        </header>
        <div className="p-3">
          <LeadTimeChart
            periods={view.periods}
            height={230}
            onPeriod={openPeriod}
            onDot={openTask}
          />
        </div>
      </section>
      {view.cut === 'none' || view.cuts === null ? (
        <LeadTimeRows
          periods={view.periods}
          quarters={view.quarters}
          unit={unit}
          onPeriod={openPeriod}
        />
      ) : (
        <LeadTimeCutTable cut={view.cut} rows={view.cuts} periods={view.periods} unit={unit} />
      )}
      {view.leftOut.length > 0 && (
        <p data-testid={ids.leftOut} className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          Left out:{' '}
          {view.leftOut.map((task, i) => (
            <span key={task.key} data-testid={ids.leftOutRow} data-key={task.key}>
              {i > 0 && '; '}
              <span className="font-mono text-ink">{task.key}</span> {task.title} (
              {task.reason.toLowerCase()})
            </span>
          ))}
          . A task with no start has no lead time; the timeline shows it as a diamond.
        </p>
      )}
    </section>
  );
};
