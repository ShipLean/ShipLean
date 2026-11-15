import type { CardDto } from '@shiplean/core/api/views';
import { actionSheetRowClass } from '../../../touch/ActionSheet';
import { Sheet } from '../../../touch/Sheet';
import { useTaskboardMoves } from '../moves/TaskboardMovesContext';
import { taskboardTestIds as ids } from '../testIds';

/** Picks who a task is assigned to: the board's people, or Unassigned. */
export function AssigneeSheet({
  card,
  rowId,
  onClose,
  onBack,
}: {
  card: CardDto;
  rowId: string;
  onClose: () => void;
  onBack: () => void;
}) {
  const { view, assign } = useTaskboardMoves();
  const people = view.peopleRows.flatMap((row) => (row.person ? [row.person] : []));
  const options = [null, ...people];
  return (
    <Sheet open title="Assignee" onClose={onClose}>
      <button
        type="button"
        data-testid={ids.actionBack}
        className={`${actionSheetRowClass} text-signal`}
        onClick={onBack}
      >
        ‹ Back
      </button>
      {options.map((person) => {
        const here = (person?.id ?? null) === (card.assignee?.id ?? null);
        return (
          <button
            key={person?.id ?? 'unassigned'}
            type="button"
            data-testid={ids.assigneeOption}
            data-person-id={person?.id ?? ''}
            aria-current={here ? 'true' : undefined}
            className={`${actionSheetRowClass} text-ink ${here ? 'bg-signal-soft font-semibold' : ''}`}
            onClick={() => {
              if (!here) assign(card, rowId, person, 'sheet');
              onClose();
            }}
          >
            {person?.name ?? 'Unassigned'}
          </button>
        );
      })}
    </Sheet>
  );
}
