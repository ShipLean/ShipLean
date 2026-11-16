import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import type { CardDto } from '@shiplean/core/api/views';
import type { SheetAction } from '../../../../touch/ActionSheet';
import { locate } from '../../moves/applyMove';
import { useBoardMoves } from '../../moves/BoardMovesContext';
import type { CardActions } from './CardActionsContext';
import { dropAtBottom, dropAtEnd, dropAtTop, dropInLane, moveTargets, otherLanes } from './moveTargets';

// The child type a Feature or an Epic lists under it, as its roll-up opens it.
const BELOW: Partial<Record<WorkItemTypeId, WorkItemTypeId>> = {
  epic: 'feature',
  feature: 'story',
};

/**
 * The rows of a card's actions sheet, as one sheet (K1): Open, Assign (Owner on Features and Epics) with who it is now,
 * what is under it (Features and Epics), then Move to (every column with its WIP, the current one ticked, over the limit in
 * the danger tone), Lane (when another lane takes drops) and Order in <column> (top, bottom). Moves go through
 * `BoardMoves.moveTo`, the desktop drop's path.
 */
export function useCardActionRows(card: CardDto, actions: CardActions): SheetAction[] {
  const moves = useBoardMoves();
  const { view } = actions;
  const at = locate(view, card.id);
  const rows: SheetAction[] = [
    {
      id: 'open',
      label: 'Open',
      icon: '↗',
      detail: 'full screen',
      onSelect: () => actions.openItem(card.key),
    },
    {
      id: 'assign',
      label: view.level === 'stories' ? 'Assign' : 'Owner',
      detail: card.assignee?.name ?? (view.level === 'stories' ? 'Unassigned' : 'No owner'),
      trailing: 'chevron',
      onSelect: () => actions.pick(card.id, 'assign'),
    },
  ];
  const belowType = BELOW[card.type.id];
  if (actions.showBelow && belowType)
    rows.push({
      id: 'below',
      label: "Show what's under it",
      trailing: 'chevron',
      onSelect: () => actions.showBelow?.(card.id, card.key, belowType),
    });
  for (const target of moveTargets(view, card.id))
    rows.push({
      id: `column:${target.columnId}:${target.done}`,
      label: target.label,
      group: 'Move to',
      checked: target.current,
      disabled: target.current,
      detail: (
        <span className={target.over ? 'font-semibold text-alert' : undefined}>
          {target.current ? `here · ${target.wip}` : target.wip}
        </span>
      ),
      onSelect: () => {
        const to = dropAtEnd(view, card.id, target);
        if (to) moves.moveTo(card.id, to);
      },
    });
  if (otherLanes(view, card.id).length > 0)
    for (const lane of view.lanes.filter((l) => l.id === at?.laneId || l.acceptsDrops)) {
      const here = lane.id === at?.laneId;
      rows.push({
        id: `lane:${lane.id}`,
        label: lane.name,
        group: 'Lane',
        checked: here,
        disabled: here,
        detail: here ? 'here' : (lane.criteriaText ?? undefined),
        onSelect: () => {
          const to = dropInLane(view, card.id, lane.id);
          if (to) moves.moveTo(card.id, to);
        },
      });
    }
  const top = dropAtTop(view, card.id);
  const bottom = dropAtBottom(view, card.id);
  const column = view.columns.find((c) => c.id === at?.columnId);
  const order = `Order in ${column?.name ?? 'column'}`;
  rows.push(
    {
      id: 'top',
      label: 'Move to top',
      group: order,
      icon: '↑',
      disabled: top === null,
      onSelect: () => top && moves.moveTo(card.id, top),
    },
    {
      id: 'bottom',
      label: 'Move to bottom',
      group: order,
      icon: '↓',
      disabled: bottom === null,
      onSelect: () => bottom && moves.moveTo(card.id, bottom),
    },
  );
  return rows;
}
