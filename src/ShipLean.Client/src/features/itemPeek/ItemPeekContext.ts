import { createContext, useContext } from 'react';

export const ItemPeekContext = createContext(false);

/** True inside the item modal, so a work item form can tell it is the modal's and not the page's. */
export function useInItemPeek(): boolean {
  return useContext(ItemPeekContext);
}
