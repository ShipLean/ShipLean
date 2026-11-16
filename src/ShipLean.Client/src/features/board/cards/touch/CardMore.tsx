import type { CardDto } from '@shiplean/core/api/views';
import { MoreButton } from '../../../../touch/MoreButton';
import { boardTestIds as ids } from '../../testIds';
import { useCardActionRows } from './CardActionRows';
import type { CardActions } from './CardActionsContext';
import { CardSheetHeader } from './CardSheetHeader';

/** The ⋯ on a card (K5 `MoreButton`): drawn only at touch widths (Card decides), never hover-only. */
export function CardMore({ card, actions }: { card: CardDto; actions: CardActions }) {
  const rows = useCardActionRows(card, actions);
  return (
    <span data-testid={ids.cardMore} className="flex-none">
      <MoreButton actions={rows} label={`${card.key} actions`}>
        <CardSheetHeader card={card} />
      </MoreButton>
    </span>
  );
}
