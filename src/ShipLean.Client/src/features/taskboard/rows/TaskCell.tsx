import { useDroppable } from '@dnd-kit/core';
import type { ReactNode } from 'react';
import type { CardDto, TaskboardViewDto } from '../../../api/views';
import { TaskCard } from '../cards/TaskCard';
import { taskboardTestIds as ids } from '../testIds';

/** One (row, state) cell and its task cards; a card dropped on it joins the cell. `children` is room for the row's add box. */
export function TaskCell({
  rowId,
  stateId,
  cards,
  view,
  showParent,
  children,
}: {
  rowId: string;
  stateId: string;
  cards: CardDto[];
  view: Pick<TaskboardViewDto, 'styleRules' | 'cardFields'>;
  showParent: boolean;
  children?: ReactNode;
}) {
  const { setNodeRef, isOver } = useDroppable({
    id: `cell:${rowId}:${stateId}`,
    data: { kind: 'cell', rowId, stateId },
  });
  return (
    <div
      ref={setNodeRef}
      data-over={isOver}
      data-testid={ids.cell}
      data-row={rowId}
      data-state={stateId}
      className="grid grid-cols-[minmax(0,1fr)] content-start gap-2 border-l border-edge bg-sunken p-2 data-[over=true]:bg-signal-soft"
    >
      {cards.map((card) => (
        <TaskCard key={card.id} card={card} rowId={rowId} view={view} showParent={showParent} />
      ))}
      {children}
    </div>
  );
}
