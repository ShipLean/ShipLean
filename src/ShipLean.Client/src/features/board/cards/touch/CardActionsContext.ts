import { createContext, useContext } from 'react';
import type { BoardViewDto } from '@shiplean/core/api/views';

/** What a card needs for its actions on a phone: provided by `BoardDnd`. */
export interface CardActions {
  /** Opens the card's actions sheet (the ⋯ button's sheet), for another trigger such as a long press. */
  open(cardId: string): void;
  /** Opens a second sheet from the actions sheet: the columns, or the other lanes. */
  pick(cardId: string, kind: 'column' | 'lane'): void;
  /** Opens the item in the modal (full screen on a phone). */
  openItem(itemKey: string): void;
  /** The board as it stands now: the sheets list its columns and lanes. */
  view: BoardViewDto;
}

export const CardActionsContext = createContext<CardActions | null>(null);

/** Null outside a board (a card drawn as a preview has no actions). */
export const useOptionalCardActions = (): CardActions | null => useContext(CardActionsContext);

export function useCardActions(): CardActions {
  const actions = useContext(CardActionsContext);
  if (!actions) throw new Error('useCardActions must sit inside BoardDnd.');
  return actions;
}
