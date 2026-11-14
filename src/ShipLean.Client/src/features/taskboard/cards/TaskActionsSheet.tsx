import type { CardDto } from '@shiplean/core/api/views';
import { actionSheetRowClass, ActionSheet } from '../../../touch/ActionSheet';
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
  onOpenItem,
}: {
  card: CardDto;
  rowId: string;
  open: boolean;
  onShow: (sheet: TaskSheet | null) => void;
  onOpenItem: () => void;
}) {
  const { view, drop } = useTaskboardMoves();
  return (
    <ActionSheet
      open={open}
      title={`${card.key} ${card.title}`}
      onClose={() => onShow(null)}
      actions={[
        { id: 'story', label: 'Move to another story ›', onSelect: () => onShow('story') },
        {
          id: 'assignee',
          label: `Assignee: ${card.assignee?.name ?? 'Unassigned'} ›`,
          onSelect: () => onShow('assignee'),
        },
        { id: 'open', label: `Open ${card.key}`, onSelect: onOpenItem },
      ]}
    >
      {(close) => (
        <>
          <p className="m-0 px-2 pt-2 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
            Move to
          </p>
          {view.columns.map(({ state }) => {
            const here = state.id === card.state.id;
            return (
              <button
                key={state.id}
                type="button"
                data-testid={ids.actionState}
                data-state-id={state.id}
                aria-current={here ? 'true' : undefined}
                className={`${actionSheetRowClass} gap-2 text-ink ${here ? 'bg-signal-soft font-semibold' : ''}`}
                onClick={() => {
                  drop(card, rowId, { stateId: state.id, rowId, beforeItemId: null }, 'sheet');
                  close();
                }}
              >
                {state.name}
                {here ? <span className="text-ink-muted">here</span> : null}
              </button>
            );
          })}
        </>
      )}
    </ActionSheet>
  );
}
