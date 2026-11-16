import type { ReactNode } from 'react';
import type { InspectionHeadlineDto, InspectionQualityStatsDto } from '@shiplean/core/api/views';
import { OVERLINE } from '../overview/overviewStyles';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { per100 } from './severity';

function Card({
  kind,
  title,
  value,
  of,
  children,
}: {
  kind: string;
  title: string;
  value: ReactNode;
  of: ReactNode;
  children?: ReactNode;
}) {
  return (
    <div
      data-testid={ids.qualityCard}
      data-kind={kind}
      className="panel grid content-start gap-2 px-4 py-3"
    >
      <span className={OVERLINE}>{title}</span>
      <div className="flex flex-wrap items-baseline gap-2">
        <b
          data-testid={ids.qualityCardValue}
          className="text-[28px] leading-none font-bold tabular-nums"
        >
          {value}
        </b>
        <span className="text-ink-muted">{of}</span>
      </div>
      <div className="flex flex-wrap gap-x-4 text-[length:var(--text-sm)] text-ink-muted">
        {children}
      </div>
    </div>
  );
}

const prs = (n: number | null) =>
  n === null ? 'no linked PR merged' : `${n} ${n === 1 ? 'PR' : 'PRs'} merged`;

/**
 * The four Quality cards for the last finished sprint (never the running one): escaping bugs, per 100 merged PRs,
 * severity-weighted, and the range's found-before-release bugs.
 */
export function QualityCards({
  headline,
  range,
}: {
  headline: InspectionHeadlineDto;
  range: InspectionQualityStatsDto;
}) {
  const { finished, quarterToDate, lastQuarter } = headline;
  const last = finished?.quality ?? null;
  const name = finished?.name ?? 'no finished sprint yet';
  return (
    <div className="grid grid-cols-[repeat(auto-fit,minmax(220px,1fr))] gap-3">
      <Card kind="escaping" title="Escaping bugs" value={last?.escaping ?? '—'} of={name}>
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
      </Card>
      <Card
        kind="per100"
        title="Per 100 merged PRs"
        value={per100(last?.per100Prs ?? null)}
        of={last === null ? name : prs(last.mergedPrs)}
      >
        <span>
          Over the range <b className="text-ink">{per100(range.per100Prs)}</b>
        </span>
        <span>PRs linked to an item only</span>
      </Card>
      <Card kind="weighted" title="Severity-weighted" value={last?.weighted ?? '—'} of={name}>
        <span>Weights are a project setting</span>
      </Card>
      <Card
        kind="foundBeforeRelease"
        title="Found before release"
        value={range.foundBeforeRelease}
        of="in the range, not counted"
      />
    </div>
  );
}
