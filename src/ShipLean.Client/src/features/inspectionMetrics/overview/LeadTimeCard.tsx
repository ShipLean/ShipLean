import type {
  InspectionHeadlineDto,
  InspectionHeadlinePeriodDto,
  InspectionPeriodDto,
} from '@shiplean/core/api/views';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { OVERLINE } from './overviewStyles';

const tasks = (n: number) => `${n} ${n === 1 ? 'task' : 'tasks'}`;

/**
 * Velocity: the mean lead time of the last finished sprint (the headline, never the running one), its change against
 * the sprint before, a bar per sprint, and the running sprint so far beside this quarter and last.
 */
export function LeadTimeCard({
  headline,
  finished,
  periods,
}: {
  headline: InspectionHeadlineDto;
  finished: InspectionHeadlinePeriodDto;
  periods: readonly InspectionPeriodDto[];
}) {
  const change = finished.leadTime.changePctVsPrevious;
  const { soFar, quarterToDate, lastQuarter } = headline;
  const max = Math.max(...periods.map((p) => p.leadTime.meanHours ?? 0), 1);
  return (
    <section
      data-testid={ids.overviewLeadCard}
      className="panel grid min-w-[360px] gap-2 px-4 py-3"
    >
      <div className="flex items-center gap-2">
        <h2 className={OVERLINE}>Velocity · lead time per task</h2>
        <span className="flex-1" />
        {change === null ? null : (
          <span
            data-testid={ids.overviewLeadCardChange}
            title="Against the sprint before"
            className={`rounded-full px-2 py-0.5 text-[length:var(--text-xs)] font-semibold ${
              change > 0 ? 'bg-alert/15 text-alert' : 'bg-good/15 text-good'
            }`}
          >
            {change > 0 ? '▲' : '▼'} {Math.abs(change)}%
          </span>
        )}
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
            className={`flex-1 rounded-t-[2px] ${
              period.id === finished.periodId
                ? 'bg-signal'
                : period.inProgress
                  ? 'bg-signal/25'
                  : 'bg-signal/50'
            }`}
            style={{ height: `${Math.max(4, ((period.leadTime.meanHours ?? 0) / max) * 100)}%` }}
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
    </section>
  );
}
