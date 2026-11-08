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
  lane = false,
  empty,
  children,
}: {
  rowId: string;
  stateId: string;
  cards: CardDto[];
  view: Pick<TaskboardViewDto, 'styleRules' | 'cardFields'>;
  showParent: boolean;
  /**
   * A phone lane's task area (SHIP-722): a touch drop target (`data-drop-target`, `data-row-id`, `data-state-id`) that shows
   * `[data-drop-active]` as a highlight, under the lane's card.
   */
  lane?: boolean;
  /** What a lane shows when no card is drawn: None, or how many the filter hides. */
  empty?: ReactNode;
  children?: ReactNode;
}) {
  const { setNodeRef, isOver } = useDroppable({
    id: `cell:${rowId}:${stateId}`,
    data: { kind: 'cell', rowId, stateId },
  });
  if (lane) {
    return (
      <div
        data-testid={ids.laneTasks}
        data-drop-target=""
        data-row-id={rowId}
        data-state-id={stateId}
        className="-mt-1.5 ml-3 grid min-h-10 grid-cols-[minmax(0,1fr)] content-start gap-1.5 rounded-b-md border border-t-0 border-edge bg-sunken px-2 pb-2 pt-3 transition-colors data-[drop-active]:bg-signal-soft data-[drop-active]:shadow-[inset_0_0_0_2px_var(--color-accent)]"
      >
        {cards.map((card) => (
          <TaskCard key={card.id} card={card} rowId={rowId} view={view} showParent={showParent} />
        ))}
        {cards.length === 0 ? empty : null}
        {children}
      </div>
    );
  }
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
