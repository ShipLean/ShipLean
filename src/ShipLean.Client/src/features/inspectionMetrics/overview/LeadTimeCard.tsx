import type {
  InspectionHeadlineDto,
  InspectionHeadlinePeriodDto,
  InspectionPeriodDto,
} from '@shiplean/core/api/views';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { OVERLINE } from './overviewStyles';

/** A change under this many percent reads as the same (C2's ChangeChip). */
const SAME_PCT = 3;

function ChangeChip({ change }: { change: number }) {
  const same = Math.abs(change) < SAME_PCT;
  // Lead time going down is better.
  const background = same
    ? undefined
    : change < 0
      ? 'color-mix(in srgb, var(--color-ok) 18%, transparent)'
      : 'color-mix(in srgb, var(--color-danger) 16%, transparent)';
  return (
    <span
      data-testid={ids.overviewLeadCardChange}
      title="Against the sprint before"
      className={`rounded-full px-2 py-0.5 text-[length:var(--text-xs)] font-semibold text-ink ${same ? 'bg-sunken' : ''}`}
      style={{ background }}
    >
      {same ? '≈ same' : `${change > 0 ? '▲' : '▼'} ${Math.abs(change)}%`}
    </span>
  );
}

const tasks = (n: number) => `${n} ${n === 1 ? 'task' : 'tasks'}`;

/**
 * Velocity: the mean lead time of the last finished sprint (the headline, never the running one), its change against
 * the sprint before, a bar per sprint, and the running sprint so far beside this quarter and last.
 */
export function LeadTimeCard({
  headline,
  finished,
  periods,
  onOpen,
}: {
  headline: InspectionHeadlineDto;
  finished: InspectionHeadlinePeriodDto;
  periods: readonly InspectionPeriodDto[];
  onOpen(): void;
}) {
  const change = finished.leadTime.changePctVsPrevious;
  const { soFar, quarterToDate, lastQuarter } = headline;
  const max = Math.max(...periods.map((p) => p.leadTime.meanHours ?? 0), 1);
  return (
    <button
      type="button"
      data-testid={ids.overviewLeadCard}
      onClick={onOpen}
      className="panel grid min-w-[360px] gap-2 px-4 py-3 text-left hover:border-signal focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-signal"
    >
      <div className="flex items-center gap-2">
        <span className={OVERLINE}>Velocity · lead time per task</span>
        <span className="flex-1" />
        {change === null ? null : <ChangeChip change={change} />}
      </div>
      <div className="flex items-baseline gap-2">
        <b data-testid={ids.overviewLeadCardValue} className="text-[28px] leading-none font-bold">
          {finished.leadTime.meanLabel ?? '—'}
        </b>
        <span className="text-ink-muted">
          mean, {finished.name} · {tasks(finished.leadTime.tasks)}
        </span>
      </div>
      <div aria-hidden="true" className="flex h-10 items-end gap-1">
        {periods.map((period) => (
          <span
            key={period.id}
            className="flex-1 rounded-t-[2px]"
            style={{
              height: `${Math.max(4, ((period.leadTime.meanHours ?? 0) / max) * 100)}%`,
              background: 'var(--chart-completed)',
              opacity: period.id === finished.periodId ? 1 : period.inProgress ? 0.25 : 0.5,
            }}
          />
        ))}
      </div>
      <div className="flex flex-wrap gap-x-4 text-[length:var(--text-sm)] text-ink-muted">
        {soFar === null ? null : (
          <span data-testid={ids.overviewLeadCardSoFar}>
            {soFar.name} so far <b className="text-ink">{soFar.leadTime.meanLabel ?? '—'}</b>
          </span>
        )}
        {quarterToDate === null ? null : (
          <span>
            {quarterToDate.quarter} to date{' '}
            <b className="text-ink">{quarterToDate.leadTime.meanLabel ?? '—'}</b>
          </span>
        )}
        {lastQuarter === null ? null : (
          <span>
            {lastQuarter.quarter}{' '}
            <b className="text-ink">{lastQuarter.leadTime.meanLabel ?? '—'}</b>
          </span>
        )}
      </div>
    </button>
  );
}
