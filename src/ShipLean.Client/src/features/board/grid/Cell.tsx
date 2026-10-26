import { useDroppable } from '@dnd-kit/core';
import type { BoardCellDto } from '../../../api/views';
import { DraggableCard } from '../cards/DraggableCard';
import { DropLine } from '../cards/DropLine';
import { boardTestIds as ids } from '../testIds';
import type { BoardContext } from './boardContext';
import { NewCard } from './NewCard';
import { ShowOlder } from './ShowOlder';
import type { Track } from './layout';

// One cell: a lane's cards in one column (or one half of a split column). The first column of the default lane also
// holds "+ New"; a Done cell offers Show older when it hides cards.
export function Cell({
  cell,
  track,
  laneId,
  context,
  offersNew,
}: {
  cell: BoardCellDto | undefined;
  track: Track;
  laneId: string;
  context: BoardContext;
  offersNew: boolean;
}) {
  const {
    projectId,
    projectKey,
    boardId,
    level,
    compact,
    settings,
    onExpand,
    flashing,
    onRollupClick,
  } = context;
  const place = { laneId, columnId: track.column.id, done: track.doneHalf };
  const { setNodeRef, isOver } = useDroppable({
    id: `cell:${laneId}:${track.column.id}:${track.doneHalf}`,
    data: { kind: 'cell', ...place },
  });
  return (
    <div
      ref={setNodeRef}
      data-testid={ids.cell}
      data-over={isOver}
      data-lane={laneId}
      data-column={track.column.id}
      data-done-half={track.doneHalf}
      className="flex min-h-16 min-w-0 flex-col gap-1.5 rounded-control p-1.5"
      style={{
        background: track.doneHalf
          ? 'color-mix(in srgb, var(--color-surface-2) 30%, transparent)'
          : 'color-mix(in srgb, var(--color-surface-2) 55%, transparent)',
        border: track.doneHalf ? '1px dashed var(--color-border)' : undefined,
        outline: isOver ? '2px solid var(--color-accent)' : undefined,
      }}
    >
      {offersNew ? <NewCard projectId={projectId} boardId={boardId} level={level} /> : null}
      {(cell?.cards ?? []).map((card) => (
        <DraggableCard
          place={place}
          key={card.id}
          card={card}
          projectKey={projectKey}
          level={level}
          compact={compact}
          settings={settings}
          inDone={track.last}
          flashing={flashing.has(card.id)}
          onRollupClick={onRollupClick && ((type) => onRollupClick(card.id, card.key, type))}
        />
      ))}
      {isOver ? <DropLine /> : null}
      {cell && cell.olderCount > 0 && cell.olderCursor !== null ? (
        <ShowOlder count={cell.olderCount} cursor={cell.olderCursor} onExpand={onExpand} />
      ) : null}
    </div>
  );
}
