import { useState } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { TouchHandle } from '../../touch/TouchHandle';
import { TouchRow } from '../../touch/TouchRow';

/**
 * One Planning row at touch: a TouchRow with its drag handle (unless a filter, search or check queue is on) and
 * Forecast's "→ Sprint n". The row fades while it is lifted. `data-pull-section` lets the pull line's handle find the
 * rows it moves past.
 */
export function TouchPlanningRow({
  row,
  projectKey,
  sectionId,
  forecast,
  flash,
  handle,
  onOver,
  onDrop,
}: {
  row: BacklogRowDto;
  projectKey: string;
  sectionId: string;
  forecast?: string;
  flash: boolean;
  // No handle while filtering: the page scrolls normally and the rank shows.
  handle: boolean;
  onOver: (rowId: string, targetId: string | null) => void;
  onDrop: (rowId: string, targetId: string | null) => void;
}) {
  const [lifted, setLifted] = useState(false);
  return (
    <div data-pull-section={sectionId} className={lifted ? 'opacity-40' : ''}>
      <TouchRow
        row={row}
        projectKey={projectKey}
        flash={flash}
        forecast={forecast}
        handle={
          handle ? (
            <TouchHandle
              rowId={row.id}
              rank={row.rank ?? ''}
              onOver={(target) => onOver(row.id, target)}
              onDrop={(target) => onDrop(row.id, target)}
              onLiftedChange={setLifted}
              ghost={
                <div className="w-[min(80vw,22rem)] truncate rounded-control border border-accent bg-panel px-3 py-3 text-[length:var(--text-sm)] font-medium shadow-lg">
                  {row.key} {row.title}
                </div>
              }
            />
          ) : undefined
        }
      />
    </div>
  );
}
