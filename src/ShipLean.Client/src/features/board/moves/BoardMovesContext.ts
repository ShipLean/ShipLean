import { createContext, useContext } from 'react';
import type { DropTarget } from './applyMove';

/** What a card and a cell need from the board to move cards: set up once by `BoardDnd`. */
export interface BoardMoves {
  drop: (itemId: string, to: DropTarget) => void;
  /** Alt + an arrow key on a focused card. True when the key was a move key (the card takes focus again once it lands). */
  keyMove: (itemId: string, cardKey: string, key: string) => boolean;
  /** The card's "Move to Done" offer: one move into the board's last column, in the card's lane. */
  finish: (itemId: string) => void;
  /** True once, for the card a keyboard move just put down: it takes focus back. */
  takeFocus: (cardKey: string) => boolean;
}

export const BoardMovesContext = createContext<BoardMoves | null>(null);

export function useBoardMoves(): BoardMoves {
  const moves = useContext(BoardMovesContext);
  if (!moves) throw new Error('A board card must sit inside BoardDnd.');
  return moves;
}
