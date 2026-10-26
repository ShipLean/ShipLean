import { createContext, useContext } from 'react';
import type { DraftLink } from './draft';

/** What the form shares with the links slot: the draft's links, whether they differ from the saved ones, and a way to replace them. */
export interface DraftLinks {
  links: DraftLink[];
  changed: boolean;
  setLinks: (links: DraftLink[]) => void;
}

export const DraftContext = createContext<DraftLinks | null>(null);

export function useDraftLinks(): DraftLinks {
  const draft = useContext(DraftContext);
  if (draft === null) throw new Error('The links editor must be rendered inside a work item form.');
  return draft;
}
