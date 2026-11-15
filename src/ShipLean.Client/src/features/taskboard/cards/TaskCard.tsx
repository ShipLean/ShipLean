import { useCallback, useState, type DragEvent, type KeyboardEvent, type MouseEvent } from 'react';
import type { CardDto, TaskboardViewDto } from '@shiplean/core/api/views';
import { useCardDrag } from '../moves/useCardDrag';
import { useTouchCardDrag } from '../moves/useTouchCardDrag';
import { useTaskboardMoves } from '../moves/TaskboardMovesContext';
import { useItemPeek } from '../../itemPeek/useItemPeek';
import { TouchDragGhost } from '../../../touch/TouchDragGhost';
import { useTouchShell } from '../../../touch/useTouchShell';
import { taskboardTestIds as ids } from '../testIds';
import { AssigneeSheet } from './AssigneeSheet';
import { MoveToStorySheet } from './MoveToStorySheet';
import { TaskActionsSheet, type TaskSheet } from './TaskActionsSheet';
import { TaskCardFace } from './TaskCardFace';

/** One task card: dragged by the pointer, moved with Alt + arrow keys, picked up with its button (unassigned cards in a Proposed state). */
export function TaskCard({
  card,
  rowId,
  view,
  showParent,
}: {
  card: CardDto;
  rowId: string;
  view: Pick<TaskboardViewDto, 'styleRules' | 'cardFields'>;
  showParent: boolean;
}) {
  const moves = useTaskboardMoves();
  const { touch } = useTouchShell();
  const { open: openItem } = useItemPeek();
  // The task ⋯ sheet, or one of its pickers; set it to 'actions' to open the sheet from elsewhere.
  const [sheet, setSheet] = useState<TaskSheet | null>(null);
  const drag = useCardDrag(card, rowId);
  // On the phone grid a long press lifts the card; held without moving it opens the actions.
  const { enabled: touchDragOn, drag: touchDrag } = useTouchCardDrag(card, rowId, () =>
    setSheet('actions'),
  );
  // A card a keyboard move just put down is a new element: it takes the focus back.
  const { ref: dragRef } = drag;
  const { ref: touchRef } = touchDrag.props;
  const ref = useCallback(
    (node: HTMLElement | null) => {
      dragRef(node);
      const stopTouch = node && touchDragOn ? touchRef(node) : undefined;
      // The old element is still focused when the board re-renders: only the one that replaces it takes the focus.
      if (node && node !== document.activeElement && moves.takeFocus(card.key)) node.focus();
      return () => {
        stopTouch?.();
        dragRef(null);
      };
    },
    [dragRef, touchRef, touchDragOn, moves, card.key],
  );
  const pickUp = card.assignee === null && card.state.category === 'proposed' ? moves.pickUp : null;
  function onKeyDown(event: KeyboardEvent) {
    if (event.altKey && moves.keyMove(card, rowId, event.key)) event.preventDefault();
  }
  // A tap on the card (not on its link or buttons) opens the item at touch widths.
  function onClick(event: MouseEvent) {
    if (!touch || event.defaultPrevented) return;
    if ((event.target as HTMLElement).closest('a, button')) return;
    openItem(card.key);
  }
  const close = () => setSheet(null);
  // Picking a row of the actions sheet closes it after it chose the next sheet: only close it if it is still the one showing.
  const closeActions = () => setSheet((now) => (now === 'actions' ? null : now));
  return (
    <>
      <TaskCardFace
        card={card}
        view={view}
        showParent={showParent}
        articleRef={ref}
        // A disabled draggable marks its card aria-disabled, which would disable the ⋯ button inside it.
        articleProps={
          touch
            ? {
                onKeyDown,
                onClick,
                // A long press on the title link would start the browser's link drag and cancel the touch: the lift is ours.
                onDragStart: touchDragOn ? (event: DragEvent) => event.preventDefault() : undefined,
              }
            : { ...drag.attributes, ...drag.listeners, onKeyDown, onClick }
        }
        style={{
          ...drag.style,
          ...(touchDragOn ? touchDrag.props.style : undefined),
          opacity: touchDrag.lifted ? 0.4 : drag.style.opacity,
        }}
        pickUp={pickUp ? () => pickUp(card, rowId) : null}
        actions={
          touch ? (
            <button
              type="button"
              data-testid={ids.cardActions}
              aria-label={`Actions for ${card.key}`}
              aria-haspopup="dialog"
              className="inline-grid size-[var(--touch-target)] flex-none place-items-center rounded-control text-[length:var(--text-md)] leading-none text-ink-muted active:bg-signal-soft"
              onClick={() => setSheet('actions')}
            >
              ⋯
            </button>
          ) : null
        }
      />
      {touchDrag.lifted ? (
        <TouchDragGhost x={touchDrag.x} y={touchDrag.y}>
          <TaskCardFace card={card} view={view} showParent={showParent} preview />
        </TouchDragGhost>
      ) : null}
      {touch ? (
        <>
          <TaskActionsSheet
            card={card}
            rowId={rowId}
            open={sheet === 'actions'}
            onShow={setSheet}
            onClose={closeActions}
            onOpenItem={() => openItem(card.key)}
          />
          {sheet === 'story' ? (
            <MoveToStorySheet
              card={card}
              rowId={rowId}
              onClose={close}
              onBack={() => setSheet('actions')}
            />
          ) : null}
          {sheet === 'assignee' ? (
            <AssigneeSheet
              card={card}
              rowId={rowId}
              onClose={close}
              onBack={() => setSheet('actions')}
            />
          ) : null}
        </>
      ) : null}
    </>
  );
}
