import type { InspectionPeriodDetailDto } from '@shiplean/core/api/views';
import { inspectionMetricsTestIds as ids } from '../testIds';

/** The sprint's head: back to Lead time, its name, dates and quarter, and the sprints either side. */
export function PeriodHeader({
  detail,
  onBack,
  onPeriod,
}: {
  detail: InspectionPeriodDetailDto;
  onBack(): void;
  onPeriod(periodId: string): void;
}) {
  return (
    <header data-testid={ids.periodHeader} className="flex flex-wrap items-center gap-3">
      <button
        type="button"
        data-testid={ids.periodBack}
        className="text-[length:var(--text-sm)] text-ink-muted hover:text-ink"
        onClick={onBack}
      >
        ← Lead time
      </button>
      <h2 className="m-0 text-[length:var(--text-lg)] font-semibold">{detail.name}</h2>
      <span className="text-[length:var(--text-sm)] text-ink-muted">
        {detail.datesLabel}
        {detail.inProgress && ' · still running'}
      </span>
      <span
        data-testid={ids.periodQuarter}
        className="rounded-full bg-sunken px-2 text-[length:var(--text-xs)] font-semibold"
      >
        {detail.quarter}
      </span>
      <span className="flex-1" />
      {detail.previous !== null && (
        <button
          type="button"
          data-testid={ids.periodPrev}
          className="btn-ghost"
          onClick={() => onPeriod(detail.previous!.id)}
        >
          ‹ {detail.previous.shortName}
        </button>
      )}
      {detail.next !== null && (
        <button
          type="button"
          data-testid={ids.periodNext}
          className="btn-ghost"
          onClick={() => onPeriod(detail.next!.id)}
        >
          {detail.next.shortName} ›
        </button>
      )}
    </header>
  );
}
