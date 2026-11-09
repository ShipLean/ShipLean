import { useEffect, useRef } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { ConflictBanner } from '../../../components/ConflictBanner';
import { backlogTestIds as ids } from '../testIds';
import { PointsInput } from './PointsInput';
import { useEstimate } from './useEstimate';

function Read({ points }: { points: number | null }) {
  return points === null ? (
    <span data-testid={ids.points} title="Not estimated" className="font-semibold text-caution">
      ?
    </span>
  ) : (
    <span data-testid={ids.points}>{points}</span>
  );
}

// The points cell of a row. In the lists, click it (or type a digit on the focused row) to estimate in place; Tree mode renders it read-only.
export function PointsCell({ row }: { row: BacklogRowDto }) {
  const estimate = useEstimate(row);
  const button = useRef<HTMLButtonElement>(null);
  const wasEditing = useRef(false);
  useEffect(() => {
    // The field closing on Esc or Enter hands focus back to the cell; a walk or a Tab has moved it on already.
    if (wasEditing.current && !estimate.isEditing && document.activeElement === document.body) {
      button.current?.focus();
    }
    wasEditing.current = estimate.isEditing;
  }, [estimate.isEditing]);

  if (!estimate.editable) return <Read points={row.points} />;
  return (
    <div className="relative inline-block">
      {estimate.isEditing ? (
        <PointsInput
          rowKey={row.key}
          start={estimate.seed ?? (estimate.shown === null ? '' : String(estimate.shown))}
          seeded={estimate.seed !== null}
          onCommit={estimate.commit}
          onCancel={estimate.cancel}
        />
      ) : (
        <button
          ref={button}
          type="button"
          data-testid={ids.points}
          tabIndex={-1}
          title={
            estimate.shown === null
              ? 'Not estimated: click or type a number'
              : 'Click or type a number'
          }
          className={`h-[22px] min-w-[34px] cursor-text rounded-control border border-transparent px-[5px] text-right font-semibold tabular-nums hover:border-edge hover:bg-panel focus-visible:border-edge focus-visible:bg-panel focus-visible:outline-none ${estimate.shown === null ? 'text-caution' : ''}`}
          onClick={estimate.open}
        >
          {estimate.shown ?? '?'}
        </button>
      )}
      {estimate.conflict ? (
        <div className="absolute right-0 top-full z-20 mt-1 w-96 text-left font-normal">
          <ConflictBanner
            changedBy={`${estimate.conflict.changedBy} (${row.key})`}
            onUseMine={estimate.useMine}
            onKeepTheirs={estimate.keepTheirs}
          />
        </div>
      ) : null}
    </div>
  );
}
