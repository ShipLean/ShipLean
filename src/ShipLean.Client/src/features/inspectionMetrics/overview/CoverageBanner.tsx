import type { InspectionCoverageDto } from '@shiplean/core/api/views';
import { inspectionMetricsTestIds as ids } from '../testIds';

/** Below this share of tasks with a commit time the banner shows (the design's warning level for the coverage chip). */
export const LOW_COVERAGE_PERCENT = 70;

/** The warning that many tasks end at Resolved because they have no commit time (design ?coverage=low). */
export function CoverageBanner({
  coverage,
  unit,
}: {
  coverage: InspectionCoverageDto;
  unit: 'sprint' | 'week';
}) {
  if (coverage.percent === null || coverage.percent >= LOW_COVERAGE_PERCENT) return null;
  const without = coverage.tasks - coverage.withCommitTime;
  return (
    <div
      data-testid={ids.coverageBanner}
      role="status"
      className="grid gap-1 rounded-panel border border-caution bg-caution/10 px-4 py-3 text-[length:var(--text-sm)]"
    >
      <b>
        {without} of {coverage.tasks} tasks have no commit time, so their lead time ends at Resolved
      </b>
      <span>
        Agents send <code>committed_at</code> with each commit on <code>report_task</code>. Tasks
        reported before that get commit times from the one-off backfill. Until it runs, those {unit}
        s read longer than they were.
      </span>
    </div>
  );
}
