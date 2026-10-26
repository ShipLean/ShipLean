import { useCallback, useEffect, useRef, useState } from 'react';
import type { WorkItemTypeId } from '../../../api/refs';
import { boardTestIds as ids } from '../testIds';
import type { OpenChildList } from './ChildList';

/**
 * Which card's child list is open, and the handler a card's roll-up button calls. The list opens under the roll-up
 * that was clicked (found by its card's id and its type); the same roll-up again closes it.
 */
const rollupOf = (itemId: string, type: WorkItemTypeId) =>
  document.querySelector<HTMLElement>(
    `[data-testid="${ids.card}"][data-item-id="${itemId}"] [data-testid="${ids.cardRollup}"][data-type="${type}"]`,
  );

export function useChildList() {
  const [open, setOpen] = useState<OpenChildList | null>(null);
  const shown = useRef(open);
  useEffect(() => {
    shown.current = open;
  }, [open]);
  // `focusBack`: the list was dismissed from the keyboard, so focus goes back to the roll-up that opened it.
  const close = useCallback((focusBack = false) => {
    const was = shown.current;
    setOpen(null);
    if (focusBack && was) rollupOf(was.itemId, was.type)?.focus();
  }, []);
  const openFor = useCallback((itemId: string, itemKey: string, type: WorkItemTypeId) => {
    const anchor = rollupOf(itemId, type);
    const box = anchor?.getBoundingClientRect();
    setOpen((current) =>
      current?.itemId === itemId && current.type === type
        ? null
        : { itemId, itemKey, type, left: box?.left ?? 8, top: (box?.bottom ?? 8) + 4 },
    );
  }, []);
  return { open, openFor, close };
}
