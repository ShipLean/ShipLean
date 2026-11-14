import type { BoardViewDto } from '@shiplean/core/api/views';
import { ActionSheet } from '../../../../touch/ActionSheet';
import { locate } from '../../moves/applyMove';
import type { CardActions } from './CardActionsContext';
import { useCardActionRows } from './CardActionRows';

/** The actions sheet opened by `useCardActions().open` (a long press): the same rows as the ⋯ button's. */
export function CardActionsSheet({
  view,
  cardId,
  actions,
  onClose,
}: {
  view: BoardViewDto;
  cardId: string | null;
  actions: CardActions;
  onClose: () => void;
}) {
  const card = cardId ? locate(view, cardId)?.card : undefined;
  return card ? <Rows card={card} actions={actions} onClose={onClose} /> : null;
}

function Rows({
  card,
  actions,
  onClose,
}: {
  card: NonNullable<ReturnType<typeof locate>>['card'];
  actions: CardActions;
  onClose: () => void;
}) {
  const rows = useCardActionRows(card, actions);
  return <ActionSheet open title={`${card.key} actions`} actions={rows} onClose={onClose} />;
}
