import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { BacklogReviewDto } from '@shiplean/core/api/backlogWrites';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { Sheet } from '../../../touch/Sheet';
import { cachedRow } from '../estimate/cachedRow';
import { useEstimate } from '../estimate/useEstimate';
import { PointsBody } from '../touch/PointsSheet';
import { backlogTestIds as ids } from '../testIds';
import { queueStep, type QueueStep } from './queueStep';

// One item of the queue. Keyed by the row, so each item gets its own estimate.
function Step({ row, step, onPass }: { row: BacklogRowDto; step: QueueStep; onPass: () => void }) {
  const estimate = useEstimate(row);
  return (
    <PointsBody
      row={row}
      estimate={estimate}
      queue={{ position: step.position, total: step.total, onSkip: onPass }}
      onSaved={onPass}
    />
  );
}

/**
 * The not-estimated queue at touch (SHIP-716 D9, SHIP-1626 BACKLOG-3): the Estimate sheet steps through the frozen rows
 * still flagged, one at a time. A pick saves and goes to the next once the server took it; Skip goes to the next with
 * nothing sent. Picked and skipped rows are kept only while the queue is open. A frozen id with no row is left out.
 */
export function TouchEstimateQueue({
  review,
  frozenIds,
  onClose,
}: {
  review: BacklogReviewDto | null;
  frozenIds: readonly string[];
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [passed, setPassed] = useState<ReadonlySet<string>>(new Set());
  const flagged = new Set(review?.checks.find((c) => c.id === 'unestimated')?.itemIds ?? []);
  const rows = new Map<string, BacklogRowDto>();
  for (const id of frozenIds) {
    const row = cachedRow(queryClient, id)?.row;
    if (row) rows.set(id, row);
  }
  const step = queueStep([...rows.keys()], flagged, passed);
  const row = step === null ? undefined : rows.get(step.id);
  return (
    <Sheet open title="Estimate" onClose={onClose}>
      <div data-testid={ids.pointsSheet} className="grid gap-3">
        {step === null || row === undefined ? (
          <p data-testid={ids.queueEmpty} role="status" className="m-0">
            Everything in the queue is estimated or skipped.
          </p>
        ) : (
          <Step
            key={row.id}
            row={row}
            step={step}
            onPass={() => setPassed((current) => new Set(current).add(row.id))}
          />
        )}
      </div>
    </Sheet>
  );
}
