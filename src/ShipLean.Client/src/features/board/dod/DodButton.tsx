import { useCallback, useEffect, useRef, useState } from 'react';
import type { BoardColumnDto } from '../../../api/views';
import { boardTestIds as ids } from '../testIds';
import { useCloseOnHorizontalScroll } from '../useCloseOnHorizontalScroll';
import { DodPopover } from './DodPopover';

const WIDTH = 290;

// The column header's ⓘ: opens the column's popover under it, right-aligned to the button and kept inside the window.
export function DodButton({ column }: { column: BoardColumnDto }) {
  const [anchor, setAnchor] = useState<{ left: number; top: number } | null>(null);
  const button = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (anchor === null) return;
    const close = (event: MouseEvent) => {
      if (
        !button.current?.contains(event.target as Node) &&
        !(event.target as Element).closest(`[data-testid="${ids.dod}"]`)
      ) {
        setAnchor(null);
      }
    };
    // Escape works wherever focus is; the opener gets focus back.
    const escape = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      setAnchor(null);
      button.current?.focus();
    };
    document.addEventListener('mousedown', close);
    document.addEventListener('keydown', escape);
    return () => {
      document.removeEventListener('mousedown', close);
      document.removeEventListener('keydown', escape);
    };
  }, [anchor]);

  // The popover is placed once, under the button: a sideways scroll of the board would leave it behind its column.
  const opener = useCallback(() => (anchor === null ? null : button.current), [anchor]);
  const closeIt = useCallback(() => setAnchor(null), []);
  useCloseOnHorizontalScroll(opener, closeIt);

  function toggle() {
    if (anchor !== null) return setAnchor(null);
    const rect = button.current!.getBoundingClientRect();
    setAnchor({
      left: Math.max(8, Math.min(rect.right - WIDTH, window.innerWidth - WIDTH - 8)),
      top: rect.bottom + 4,
    });
  }

  function close() {
    setAnchor(null);
    button.current?.focus();
  }

  return (
    <>
      <button
        ref={button}
        type="button"
        data-testid={ids.dodOpen}
        aria-haspopup="dialog"
        aria-expanded={anchor !== null}
        aria-label={`${column.name}: state per type and definition of done`}
        className="ml-auto inline-grid size-5 flex-none cursor-pointer place-items-center rounded-full border-0 bg-transparent p-0 text-ink-muted hover:bg-sunken hover:text-ink"
        onClick={toggle}
      >
        <span aria-hidden="true">ⓘ</span>
      </button>
      {anchor === null ? null : <DodPopover column={column} anchor={anchor} onClose={close} />}
    </>
  );
}
