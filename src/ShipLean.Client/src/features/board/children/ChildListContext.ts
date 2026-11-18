import { createContext, useContext } from 'react';
import type { Level } from '../../../routes';
import type { OpenChildList } from './ChildList';

/** The open child list and what it needs, for a card that draws the list inside itself at touch widths. */
export interface ChildListHost {
  open: OpenChildList | null;
  projectId: string;
  projectKey: string;
  boardId: string;
  level: Level;
  close: (focusBack?: boolean) => void;
}

export const ChildListContext = createContext<ChildListHost | null>(null);

/** Null outside a board (a card drawn as a preview has no list). */
export const useOptionalChildList = (): ChildListHost | null => useContext(ChildListContext);
