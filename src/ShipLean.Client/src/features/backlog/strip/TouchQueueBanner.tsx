import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { BacklogReviewDto } from '@shiplean/core/api/backlogWrites';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { cachedRow } from '../estimate/cachedRow';
import { useEstimate } from '../estimate/useEstimate';
import { PointsSheet } from '../touch/PointsSheet';
import { backlogTestIds as ids } from '../testIds';

function Step({
  row,
  position,
  total,
  onSkip,
}: {
  row: BacklogRowDto;
  position: number;
  total: number;
  onSkip: () => void;
}) {
  const estimate = useEstimate(row);
  const [open, setOpen] = useState(false);
  return (
    <>
      <div className="min-w-0 flex-1">
        <div className="text-[length:var(--text-xs)] text-ink-muted tabular-nums">
          Not estimated, {position} of {total}
        </div>
        <div className="truncate">
          <span data-testid={ids.queueStepKey} className="font-semibold">
            {row.key}
          </span>{' '}
          {row.title}
        </div>
      </div>
      <button
        type="button"
        data-testid={ids.queuePoints}
        disabled={!estimate.editable}
        className="min-h-[var(--touch-target)] rounded-control border border-signal px-3 font-medium text-signal disabled:border-edge disabled:text-ink-muted"
        onClick={() => setOpen(true)}
      >
        Points
      </button>
      <button
        type="button"
        data-testid={ids.queueSkip}
        className="min-h-[var(--touch-target)] rounded-control border border-edge px-3 font-medium text-ink-muted"
        onClick={onSkip}
      >
        Skip
      </button>
      <PointsSheet row={row} estimate={estimate} open={open} onClose={() => setOpen(false)} />
    </>
  );
}

/**
 * The not-estimated queue at touch (SHIP-716 D9): a banner that steps through the frozen rows still flagged, one at a
 * time, with the Points sheet and Skip. Skipping is kept only while the queue is open. A row estimated leaves the
 * flagged set when the backlog is read again, which moves the banner to the next.
 */
export function TouchQueueBanner({
  review,
  frozenIds,
}: {
  review: BacklogReviewDto | null;
  frozenIds: readonly string[];
}) {
  const queryClient = useQueryClient();
  const [skipped, setSkipped] = useState<ReadonlySet<string>>(new Set());
  const flagged = new Set(review?.checks.find((c) => c.id === 'unestimated')?.itemIds ?? []);
  const left = frozenIds.filter((id) => flagged.has(id) && !skipped.has(id));
  const currentId = left[0];
  const row = currentId === undefined ? null : (cachedRow(queryClient, currentId)?.row ?? null);
  return (
    <div
      data-testid={ids.queueBanner}
      role="status"
      className="flex min-h-11 items-center gap-2 border-b border-edge bg-signal-soft px-3 py-1 text-[length:var(--text-sm)]"
    >
      {row === null ? (
        <span>Everything in the queue is estimated or skipped.</span>
      ) : (
        <Step
          key={row.id}
          row={row}
          position={frozenIds.indexOf(row.id) + 1}
          total={frozenIds.length}
          onSkip={() => setSkipped((current) => new Set(current).add(row.id))}
        />
      )}
    </div>
  );
}
