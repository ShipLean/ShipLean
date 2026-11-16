import type { InspectionPeriodDto, InspectionQualityStatsDto } from '@shiplean/core/api/views';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { per100, SEVERITY_FILLS, SEVERITY_LABELS, SEVERITY_PARTS, severityCount } from './severity';

/** The plot's height in px; each count is a share of it against the tallest bar (at least 4, so one bug isn't full height). */
const PLOT = 160;
const MIN_SCALE = 4;

const total = (q: InspectionQualityStatsDto) => q.escaping + q.foundBeforeRelease;

/**
 * Escaping bugs per period, stacked by the server's severity counts (sev 1 at the bottom), found-before-release bugs as a
 * dashed box on top. The count sits above each bar; the period's short name and its per-100-merged-PRs below.
 */
export function QualityChart({ periods }: { periods: readonly InspectionPeriodDto[] }) {
  const shown = periods.filter(
    (period): period is InspectionPeriodDto & { quality: InspectionQualityStatsDto } =>
      period.quality !== null,
  );
  const scale = Math.max(MIN_SCALE, ...shown.map((p) => total(p.quality)));
  const px = (n: number) => (n / scale) * PLOT;
  return (
    <div
      data-testid={ids.qualityChart}
      role="img"
      aria-label="Escaping bugs per period by severity"
      className="flex items-end gap-2 border-b border-edge"
      style={{ height: PLOT + 24 }}
    >
      {shown.map((period) => {
        const q = period.quality;
        return (
          <div
            key={period.id}
            data-testid={ids.qualityChartBar}
            data-period-id={period.id}
            title={`${period.name}: ${q.escaping} escaping, ${q.foundBeforeRelease} found before release`}
            className="flex flex-1 flex-col items-center justify-end"
          >
            <b className="mb-1 text-[length:var(--text-xs)] tabular-nums">{q.escaping}</b>
            <div className="flex w-full max-w-10 flex-col-reverse">
              {SEVERITY_PARTS.map((part) => {
                const n = severityCount(q.escapingBySeverity, part);
                return n === 0 ? null : (
                  <span
                    key={part}
                    data-testid={ids.qualityChartSegment}
                    data-sev={part}
                    className="block rounded-[2px] border-t border-panel"
                    style={{ height: px(n), background: SEVERITY_FILLS[part] }}
                  />
                );
              })}
              {q.foundBeforeRelease > 0 && (
                <span
                  data-testid={ids.qualityChartSegment}
                  data-sev="foundBeforeRelease"
                  className="box-border block rounded-[2px] border border-dashed border-ink-muted"
                  style={{ height: px(q.foundBeforeRelease) }}
                />
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}

/** The period labels under the chart, in the same columns: short name and per 100 merged PRs. */
export function QualityChartAxis({ periods }: { periods: readonly InspectionPeriodDto[] }) {
  return (
    <div className="flex gap-2 pt-1">
      {periods
        .filter((period) => period.quality !== null)
        .map((period) => (
          <div
            key={period.id}
            className="flex flex-1 flex-col items-center text-[length:var(--text-xs)] tabular-nums"
          >
            <span className="font-medium">
              {period.shortName}
              {period.inProgress && <span className="text-ink-muted"> · so far</span>}
            </span>
            <span className="text-ink-muted">{per100(period.quality?.per100Prs ?? null)}</span>
          </div>
        ))}
    </div>
  );
}

export function QualityLegend() {
  return (
    <span className="flex flex-wrap items-center gap-3 text-[length:var(--text-xs)] text-ink-muted">
      {SEVERITY_PARTS.map((part) => (
        <span key={part} className="inline-flex items-center gap-1">
          <i
            aria-hidden="true"
            className="inline-block size-2.5 rounded-[2px]"
            style={{ background: SEVERITY_FILLS[part] }}
          />
          {SEVERITY_LABELS[part]}
        </span>
      ))}
      <span className="inline-flex items-center gap-1">
        <i
          aria-hidden="true"
          className="box-border inline-block size-2.5 rounded-[2px] border border-dashed border-ink-muted"
        />
        Found before release
      </span>
    </span>
  );
}
