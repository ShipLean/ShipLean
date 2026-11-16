import type { BoardViewDto } from '@shiplean/core/api/views';
import { Avatar } from '../../../../components/Avatar';
import { actionSheetCheckedClass, actionSheetRowClass } from '../../../../touch/ActionSheet';
import { Sheet } from '../../../../touch/Sheet';
import { useMe } from '../../../shell/useShellQueries';
import { useProjectMembers } from '../../../workItem/workItemApi';
import { locate } from '../../moves/applyMove';
import { boardTestIds as ids } from '../../testIds';
import { useAssignCard } from './useAssignCard';

/** Picks who a card is assigned to (its Owner on Features and Epics): the project's members with their avatars, then nobody last. */
export function AssignSheet({
  view,
  cardId,
  onClose,
}: {
  view: BoardViewDto;
  cardId: string;
  onClose: () => void;
}) {
  const card = locate(view, cardId)?.card;
  const members = useProjectMembers(view.projectId);
  const assign = useAssignCard(view.projectId, view.boardId);
  const owner = view.level !== 'stories';
  const people = (members.data ?? []).map((member) => member.user);
  const me = useMe().data?.id;
  const options = [...people, null];
  return (
    <Sheet
      open={card !== undefined}
      title={`${owner ? 'Owner' : 'Assign'} ${card?.key ?? ''}`.trim()}
      onClose={onClose}
    >
      <div data-testid={ids.assignSheet} className="grid">
        {options.map((person) => {
          const here = (person?.id ?? null) === (card?.assignee?.id ?? null);
          return (
            <button
              key={person?.id ?? 'unassigned'}
              type="button"
              data-testid={ids.assignOption}
              data-person-id={person?.id ?? ''}
              aria-current={here ? 'true' : undefined}
              className={`${actionSheetRowClass} gap-3 text-ink ${here ? `${actionSheetCheckedClass} font-semibold` : ''}`}
              onClick={() => {
                if (card && !here) assign(card, person);
                onClose();
              }}
            >
              <Avatar {...(person ?? { initials: null })} size="lg" />
              <span>{person?.name ?? (owner ? 'No owner' : 'Unassigned')}</span>
              {person && person.id === me ? <span className="text-ink-muted">(you)</span> : null}
            </button>
          );
        })}
      </div>
    </Sheet>
  );
}
