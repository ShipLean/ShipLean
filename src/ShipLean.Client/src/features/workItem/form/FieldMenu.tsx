import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Sheet } from '../../../touch/Sheet';
import { useTouchShell } from '../../../touch/useTouchShell';
import { workItemTestIds as ids } from '../testIds';

export const fieldButtonClass =
  'inline-flex h-7 min-w-24 items-center gap-2 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-signal';

// In the sheet every caller's rows (menu items, radio items, a StateMenu's options) are at least --touch-target high
// and read at the base size; a list that draws its own panel loses its border and shadow, as the sheet is the panel.
const sheetMenuClass =
  'grid gap-px [&_[role=listbox]]:border-0 [&_[role=listbox]]:p-0 [&_[role=listbox]]:shadow-none [&_[role=menuitem]]:min-h-[var(--touch-target)] [&_[role=menuitemradio]]:min-h-[var(--touch-target)] [&_[role=menuitemradio]]:text-[length:var(--text-base)] [&_[role=option]]:min-h-[var(--touch-target)] [&_[role=option]]:text-[length:var(--text-base)]';

// A button that opens a panel under it. Esc and a click outside close it; the panel's content gets `close`. At 900px
// and below the content opens in a bottom sheet titled with the button's label instead (SHIP-805 rule 4, C4).
export function FieldMenu({
  buttonTestId,
  buttonLabel,
  button,
  children,
}: {
  buttonTestId: string;
  buttonLabel: string;
  button: ReactNode;
  children: (close: () => void) => ReactNode;
}) {
  const { touch } = useTouchShell();
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLSpanElement>(null);
  useEffect(() => {
    // The sheet is portalled out of root and closes itself (scrim, Close, Esc, swipe).
    if (!open || touch) return undefined;
    const onPointer = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener('pointerdown', onPointer);
    return () => document.removeEventListener('pointerdown', onPointer);
  }, [open, touch]);
  const close = () => setOpen(false);
  return (
    <span
      ref={root}
      className="relative inline-flex touch:min-w-0"
      onKeyDown={(event) => {
        if (event.key === 'Escape' && open) {
          event.stopPropagation();
          setOpen(false);
        }
      }}
    >
      <button
        type="button"
        data-testid={buttonTestId}
        aria-label={buttonLabel}
        aria-haspopup={touch ? 'dialog' : 'menu'}
        aria-expanded={open}
        className={`${fieldButtonClass} touch:h-9 touch:min-w-0 touch:flex-1 touch:text-[length:var(--text-base)]`}
        onClick={() => setOpen((was) => !was)}
      >
        {button}
        <span aria-hidden="true" className="text-ink-muted touch:ml-auto">
          ▾
        </span>
      </button>
      {open && !touch ? (
        <div
          data-testid={ids.menu}
          role="menu"
          className="absolute top-full left-0 z-30 mt-1 grid min-w-56 gap-px rounded-panel border border-edge bg-panel p-1 shadow-panel"
        >
          {children(close)}
        </div>
      ) : null}
      {touch ? (
        <Sheet open={open} title={buttonLabel} onClose={close}>
          <div data-testid={ids.menu} role="menu" className={sheetMenuClass}>
            {children(close)}
          </div>
        </Sheet>
      ) : null}
    </span>
  );
}
