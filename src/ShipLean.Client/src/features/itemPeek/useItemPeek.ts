import { useCallback } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

export interface ItemPeek {
  /** The key in the address's `item` query, undefined when no modal is open. */
  itemKey: string | undefined;
  /** Opens that item in the modal over the current page (pushes a history entry, so Back closes or steps back). */
  open: (itemKey: string) => void;
  /** Closes the modal: Back when `open` pushed it, otherwise the address without `item` (a shared link). */
  close: () => void;
}

// What `open` leaves on its history entry: how many entries in a row it pushed for the modal (a swap from one item
// to another is one more), so closing can go back past all of them to the page underneath.
interface PeekState {
  itemPeek: true;
  itemPeekDepth: number;
}

const peekDepth = (state: unknown): number => {
  const mark = state as Partial<PeekState> | null;
  return mark?.itemPeek === true && typeof mark.itemPeekDepth === 'number' ? mark.itemPeekDepth : 0;
};

export function useItemPeek(): ItemPeek {
  const { pathname, search, state } = useLocation();
  const navigate = useNavigate();
  const raw = new URLSearchParams(search).get('item');
  const itemKey = raw === null || raw === '' ? undefined : raw;
  const depth = peekDepth(state);

  const open = useCallback(
    (key: string) => {
      const query = new URLSearchParams(search);
      if (query.get('item') === key) return;
      const was = query.has('item') ? depth : 0;
      query.set('item', key);
      const next: PeekState = { itemPeek: true, itemPeekDepth: was + 1 };
      void navigate({ pathname, search: `?${query.toString()}` }, { state: next });
    },
    [navigate, pathname, search, depth],
  );

  const close = useCallback(() => {
    if (depth > 0) {
      void navigate(-depth);
      return;
    }
    const query = new URLSearchParams(search);
    query.delete('item');
    const rest = query.toString();
    void navigate({ pathname, search: rest === '' ? '' : `?${rest}` }, { replace: true });
  }, [navigate, pathname, search, depth]);

  return { itemKey, open, close };
}
