import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import type { CardDto } from '@shiplean/core/api/views';
import { MoreButton } from '../../../../touch/MoreButton';
import { boardTestIds as ids } from '../../testIds';
import { useCardActionRows } from './CardActionRows';
import type { CardActions } from './CardActionsContext';

/** The ⋯ on a card (K5 `MoreButton`): drawn only at touch widths (Card decides), never hover-only. */
export function CardMore({
  card,
  actions,
  showBelow,
  belowType,
}: {
  card: CardDto;
  actions: CardActions;
  showBelow?: (type: WorkItemTypeId) => void;
  belowType?: WorkItemTypeId;
}) {
  const rows = useCardActionRows(card, actions, showBelow, belowType);
  return (
    <span data-testid={ids.cardMore} className="ml-auto contents">
      <MoreButton actions={rows} label={`${card.key} actions`} />
    </span>
  );
}
