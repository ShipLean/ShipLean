import { timelineKpis } from './timelineKpis';
import { timelineTestIds as ids } from './testIds';
import type { TimelineKpisProps } from './TimelineKpisProps';

const DASH = '—';

function Kpi({ value, unit, label }: { value: string; unit: string; label: string }) {
  return (
    <div data-testid={ids.kpi} className="grid gap-px">
      <div className="text-[length:18px] font-semibold tabular-nums">
        {value}
        {value === DASH ? null : (
          <small className="ml-1 text-[length:var(--text-xs)] font-normal text-ink-muted">
            {unit}
          </small>
        )}
      </div>
      <div className="text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted">{label}</div>
    </div>
  );
}

/** The four numbers that frame the chart: lead time, execution, tasks started and reviews. */
export function TimelineKpis({ view }: TimelineKpisProps) {
  const k = timelineKpis(view);
  const finished = view.keyDates.some((d) => d.point === 'finished' && d.at !== null);
  const startedLabel = [
    k.notStarted > 0 ? `${k.notStarted} not started` : 'started',
    k.noStart > 0 ? `${k.noStart} with no start` : null,
  ]
    .filter(Boolean)
    .join(' · ');
  const reviewsLabel =
    k.reviews === 0
      ? 'none yet'
      : k.changesRequested > 0
        ? `${k.changesRequested} asked for changes`
        : 'all approved first time';
  return (
    <div data-testid={ids.kpis} className="flex flex-wrap items-end gap-x-6 gap-y-2">
      <Kpi
        value={k.leadDays === null ? DASH : String(k.leadDays)}
        unit="days"
        label={`Lead time, Added → ${finished ? 'Finished' : 'now'}`}
      />
      <Kpi
        value={k.executionDays === null ? DASH : String(k.executionDays)}
        unit="days"
        label={finished ? 'Execution' : 'Execution so far'}
      />
      <Kpi
        value={String(k.tasksStarted)}
        unit={`of ${k.tasksTotal} tasks`}
        label={startedLabel}
      />
      <Kpi value={String(k.reviews)} unit="reviews" label={reviewsLabel} />
    </div>
  );
}
