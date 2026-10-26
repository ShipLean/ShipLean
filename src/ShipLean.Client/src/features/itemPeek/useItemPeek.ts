import { useLocation } from 'react-router-dom';

export interface ItemPeek {
  /** The key in the address's `item` query, undefined when no modal is open. */
  itemKey: string | undefined;
  /** Opens that item in the modal over the current page (pushes a history entry). */
  open: (itemKey: string) => void;
  /** Closes the modal: Back when `open` pushed it, otherwise the address without `item`. */
  close: () => void;
}

// TODO: stub for the contract push; the real behaviour (push, mark, close) replaces it in the next A0 push.
export function useItemPeek(): ItemPeek {
  const { search } = useLocation();
  const item = new URLSearchParams(search).get('item');
  return {
    itemKey: item === null || item === '' ? undefined : item,
    open: () => undefined,
    close: () => undefined,
  };
}
