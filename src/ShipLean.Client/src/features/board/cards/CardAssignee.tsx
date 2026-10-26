import { Avatar } from '../../../components/Avatar';
import type { CardDto } from '../../../api/views';
import { boardTestIds as ids } from '../testIds';

// The assignee (the Owner on Features and Epics); an unassigned card shows the dashed avatar.
export function CardAssignee({ card }: { card: CardDto }) {
  return (
    <span data-testid={ids.cardAssignee} data-assigned={card.assignee !== null}>
      <Avatar {...(card.assignee ?? {})} />
    </span>
  );
}
