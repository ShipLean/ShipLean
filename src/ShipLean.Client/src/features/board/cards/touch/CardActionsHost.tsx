import { useMemo, useState, type ReactNode } from 'react';
import type { BoardViewDto } from '@shiplean/core/api/views';
import { useItemPeek } from '../../../itemPeek/useItemPeek';
import { CardActionsContext, type CardActions } from './CardActionsContext';
import { CardActionsSheet } from './CardActionsSheet';
import { CardMoveToSheet } from './CardMoveToSheet';

type Picker = { cardId: string; kind: 'column' | 'lane' };

/** Provides `useCardActions` and holds the sheets the actions open: the actions sheet, then Move to or Lane. */
export function CardActionsHost({
  view,
  onRollupClick,
  children,
}: {
  view: BoardViewDto;
  onRollupClick?: CardActions['showBelow'];
  children: ReactNode;
}) {
  const peek = useItemPeek();
  const [actionsFor, setActionsFor] = useState<string | null>(null);
  const [picker, setPicker] = useState<Picker | null>(null);
  const openItem = peek.open;
  const value = useMemo<CardActions>(
    () => ({
      open: setActionsFor,
      pick: (cardId, kind) => setPicker({ cardId, kind }),
      openItem,
      view,
      showBelow: onRollupClick,
    }),
    [openItem, view, onRollupClick],
  );
  return (
    <CardActionsContext.Provider value={value}>
      {children}
      <CardActionsSheet
        view={view}
        cardId={actionsFor}
        actions={value}
        onClose={() => setActionsFor(null)}
      />
      {picker ? (
        <CardMoveToSheet
          view={view}
          cardId={picker.cardId}
          kind={picker.kind}
          onClose={() => setPicker(null)}
        />
      ) : null}
    </CardActionsContext.Provider>
  );
}
