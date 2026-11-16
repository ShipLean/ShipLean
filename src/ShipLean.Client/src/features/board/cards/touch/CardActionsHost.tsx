import { useMemo, useState, type ReactNode } from 'react';
import type { BoardViewDto } from '@shiplean/core/api/views';
import { useItemPeek } from '../../../itemPeek/useItemPeek';
import { CardActionsContext, type CardActions } from './CardActionsContext';
import { AssignSheet } from './AssignSheet';
import { CardActionsSheet } from './CardActionsSheet';

type Picker = { cardId: string; kind: 'assign' };

/** Provides `useCardActions` and holds the sheets the actions open: the actions sheet, then Assign. */
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
        <AssignSheet view={view} cardId={picker.cardId} onClose={() => setPicker(null)} />
      ) : null}
    </CardActionsContext.Provider>
  );
}
