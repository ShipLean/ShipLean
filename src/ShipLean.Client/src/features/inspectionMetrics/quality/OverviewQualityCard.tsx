import type { InspectionMetricsViewDto } from '@shiplean/core/api/views';
import { ChangeChip } from '../overview/LeadTimeCard';
import { OVERLINE } from '../overview/overviewStyles';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { per100 } from './severity';

const CARD =
  'panel grid min-w-[360px] content-start gap-2 px-4 py-3 text-left hover:border-signal focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-signal';

/**
 * The Overview's quality card (design ?view=overview, "Quality · escaping bugs"): the last finished sprint's escaping bugs
 * and per 100 merged PRs with the change against the sprint before, a bar per sprint, and severity-weighted beside the running sprint, this quarter and last. With no
 * bug in the project it says so. Opens ?view=quality.
 */
export function OverviewQualityCard({
  view,
  onOpen,
}: {
  view: InspectionMetricsViewDto;
  onOpen(): void;
}) {
  const { finished, soFar, quarterToDate, lastQuarter } = view.headline;
  const last = finished?.quality ?? null;
  if (view.quality === null || finished === null || last === null) {
    return (
      <button type="button" data-testid={ids.overviewQualityCard} onClick={onOpen} className={CARD}>
        <span className={OVERLINE}>Quality · escaping bugs</span>
        <span data-testid={ids.overviewQualityEmpty} className="text-ink-muted">
          No bugs yet. Escaping bugs show here once the project has bugs.
        </span>
      </button>
    );
  }
  const max = Math.max(...view.periods.map((p) => p.quality?.escaping ?? 0), 1);
  return (
    <button type="button" data-testid={ids.overviewQualityCard} onClick={onOpen} className={CARD}>
      <span className={OVERLINE}>Quality · escaping bugs</span>
      <div className="flex items-baseline gap-2">
        <b
          data-testid={ids.overviewQualityCardValue}
          className="text-[28px] leading-none font-bold"
        >
          {last.escaping}
        </b>
        <span className="text-ink-muted">
          in {finished.name} · {per100(last.per100Prs)} per 100 merged PRs
        </span>
        {last.changePctVsPrevious === null ? null : (
          <ChangeChip change={last.changePctVsPrevious} testId={ids.overviewQualityCardChange} />
        )}
      </div>
      <div
        data-testid={ids.overviewQualityBySprint}
        aria-hidden="true"
        className="flex h-10 items-end gap-1"
      >
        {view.periods.map((period) => (
          <span
            key={period.id}
            data-period-id={period.id}
            className="flex-1 rounded-t-[2px]"
            style={{
              height: `${Math.max(4, ((period.quality?.escaping ?? 0) / max) * 100)}%`,
              background: 'var(--type-bug)',
              opacity: period.id === finished.periodId ? 0.9 : period.inProgress ? 0.25 : 0.5,
            }}
          />
        ))}
      </div>
      <div className="flex flex-wrap gap-x-4 text-[length:var(--text-sm)] text-ink-muted">
        <span>
          Severity-weighted <b className="text-ink">{last.weighted}</b>
        </span>
        {soFar !== null && soFar.quality !== null && (
          <span data-testid={ids.overviewQualityCardSoFar}>
            {soFar.name} so far <b className="text-ink">{soFar.quality.escaping}</b>
          </span>
        )}
        {quarterToDate !== null && quarterToDate.quality !== null && (
          <span>
            {quarterToDate.quarter} to date{' '}
            <b className="text-ink">{quarterToDate.quality.escaping}</b>
          </span>
        )}
        {lastQuarter !== null && lastQuarter.quality !== null && (
          <span>
            {lastQuarter.quarter} <b className="text-ink">{lastQuarter.quality.escaping}</b>
          </span>
        )}
      </div>
    </button>
  );
}
