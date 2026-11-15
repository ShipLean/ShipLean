import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import type { CardDto } from '@shiplean/core/api/views';
import type { SheetAction } from '../../../../touch/ActionSheet';
import { useBoardMoves } from '../../moves/BoardMovesContext';
import type { CardActions } from './CardActionsContext';
import { dropAtBottom, dropAtTop, otherLanes } from './moveTargets';

// The child type a Feature or an Epic lists under it, as its roll-up opens it.
const BELOW: Partial<Record<WorkItemTypeId, WorkItemTypeId>> = {
  epic: 'feature',
  feature: 'story',
};

/**
 * The rows of a card's actions sheet: Open, what is under it (Features and Epics), Move to, Lane (when another lane
 * takes drops), Move to top and bottom. Moves go through `BoardMoves.moveTo`, the desktop drop's path.
 */
export function useCardActionRows(card: CardDto, actions: CardActions): SheetAction[] {
  const moves = useBoardMoves();
  const { view } = actions;
  const rows: SheetAction[] = [
    { id: 'open', label: 'Open', onSelect: () => actions.openItem(card.key) },
  ];
  const belowType = BELOW[card.type.id];
  if (actions.showBelow && belowType)
    rows.push({
      id: 'below',
      label: "Show what's under it",
      onSelect: () => actions.showBelow?.(card.id, card.key, belowType),
    });
  rows.push({ id: 'move-to', label: 'Move to', onSelect: () => actions.pick(card.id, 'column') });
  if (otherLanes(view, card.id).length > 0)
    rows.push({ id: 'lane', label: 'Lane', onSelect: () => actions.pick(card.id, 'lane') });
  const top = dropAtTop(view, card.id);
  const bottom = dropAtBottom(view, card.id);
  rows.push(
    {
      id: 'top',
      label: 'Move to top',
      disabled: top === null,
      onSelect: () => top && moves.moveTo(card.id, top),
    },
    {
      id: 'bottom',
      label: 'Move to bottom',
      disabled: bottom === null,
      onSelect: () => bottom && moves.moveTo(card.id, bottom),
    },
  );
  return rows;
}
