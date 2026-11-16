import type { CardDto } from '@shiplean/core/api/views';
import { StateDot } from '../../../components/StateDot';
import {
  ActionSheet,
  ActionSheetGroupHeading,
  ActionSheetRowFace,
  actionSheetCheckedClass,
  actionSheetRowClass,
} from '../../../touch/ActionSheet';
import { useTaskboardMoves } from '../moves/TaskboardMovesContext';
import { taskboardTestIds as ids } from '../testIds';

/** Which sheet of a card's ⋯ is open: its actions, or one of the two pickers they lead to. */
export type TaskSheet = 'actions' | 'story' | 'assignee';

/** The task ⋯ sheet (K5 `ActionSheet`): move to a state, then the story and assignee pickers and Open. */
export function TaskActionsSheet({
  card,
  rowId,
  open,
  onShow,
  onClose,
  onOpenItem,
}: {
  card: CardDto;
  rowId: string;
  open: boolean;
  onShow: (sheet: TaskSheet | null) => void;
  onClose: () => void;
  onOpenItem: () => void;
}) {
  const { view, drop, pickUp } = useTaskboardMoves();
  const canPickUp = pickUp !== null && card.assignee === null && card.state.category === 'proposed';
  return (
    <ActionSheet
      open={open}
      title={`${card.key} ${card.title}`}
      onClose={onClose}
      footer="Press and hold a card on the board to drag it to another state or lane."
      actions={[
        {
          id: 'story',
          label: 'Move to another story',
          hint: card.parent ? `${card.parent.key} ${card.parent.title}` : 'No parent',
          trailing: 'chevron',
          onSelect: () => onShow('story'),
        },
        {
          id: 'assignee',
          label: 'Assignee',
          detail: card.assignee?.name ?? 'Unassigned',
          trailing: 'chevron',
          onSelect: () => onShow('assignee'),
        },
        {
          id: 'open',
          label: `Open ${card.key}`,
          hint: 'Full screen',
          detail: '↗',
          onSelect: onOpenItem,
        },
      ]}
    >
      {(close) => (
        <>
          {canPickUp ? (
            <button
              type="button"
              data-testid={ids.actionPickUp}
              className={`${actionSheetRowClass} font-semibold text-signal`}
              onClick={() => {
                pickUp(card, rowId);
                close();
              }}
            >
              <ActionSheetRowFace
                face={{ label: 'Pick up', hint: 'Assigns you and moves it to Active' }}
              />
            </button>
          ) : null}
          {canPickUp ? <ActionSheetGroupHeading label="" divider /> : null}
          <ActionSheetGroupHeading label="Move to" divider={false} />
          {view.columns.map(({ state }) => {
            const here = state.id === card.state.id;
            return (
              <button
                key={state.id}
                type="button"
                data-testid={ids.actionState}
                data-state-id={state.id}
                aria-current={here ? 'true' : undefined}
                className={`${actionSheetRowClass} gap-3 text-ink ${here ? actionSheetCheckedClass : ''}`}
                onClick={() => {
                  drop(card, rowId, { stateId: state.id, rowId, beforeItemId: null }, 'sheet');
                  close();
                }}
              >
                <ActionSheetRowFace
                  face={{
                    label: state.name,
                    icon: <StateDot category={state.category} />,
                    detail: here ? 'here' : undefined,
                    checked: here,
                  }}
                />
              </button>
            );
          })}
          <ActionSheetGroupHeading label="" divider />
        </>
      )}
    </ActionSheet>
  );
}
