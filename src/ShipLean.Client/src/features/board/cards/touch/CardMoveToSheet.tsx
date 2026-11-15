import type { BoardViewDto } from '@shiplean/core/api/views';
import { ActionSheet, type SheetAction } from '../../../../touch/ActionSheet';
import { locate } from '../../moves/applyMove';
import { useBoardMoves } from '../../moves/BoardMovesContext';
import { dropAtEnd, dropInLane, moveTargets, otherLanes } from './moveTargets';

/**
 * The second sheet of the card actions: "Move to" lists every column (a split one as Doing and Done) with its WIP;
 * "Lane" lists the other lanes. A pick goes through `BoardMoves.moveTo`.
 */
export function CardMoveToSheet({
  view,
  cardId,
  kind,
  onClose,
}: {
  view: BoardViewDto;
  cardId: string;
  kind: 'column' | 'lane';
  onClose: () => void;
}) {
  const moves = useBoardMoves();
  const card = locate(view, cardId)?.card;
  const rows: SheetAction[] =
    kind === 'column'
      ? moveTargets(view, cardId).map((target) => ({
          id: `column:${target.columnId}:${target.done}`,
          label: `${target.label} · ${target.wip}${target.current ? ' · here' : ''}`,
          disabled: target.current,
          onSelect: () => {
            const to = dropAtEnd(view, cardId, target);
            if (to) moves.moveTo(cardId, to);
          },
        }))
      : otherLanes(view, cardId).map((lane) => ({
          id: `lane:${lane.id}`,
          label: lane.name,
          onSelect: () => {
            const to = dropInLane(view, cardId, lane.id);
            if (to) moves.moveTo(cardId, to);
          },
        }));
  return (
    <ActionSheet
      open={card !== undefined}
      title={kind === 'column' ? 'Move to' : 'Lane'}
      actions={rows}
      onClose={onClose}
    />
  );
}
