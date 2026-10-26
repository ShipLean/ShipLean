import { useEffect, useRef, useState, type ReactNode } from 'react';
import { workItemTestIds as ids } from '../testIds';

export const fieldButtonClass =
  'inline-flex h-7 min-w-24 items-center gap-2 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-signal';

// A button that opens a panel under it. Esc and a click outside close it; the panel's content gets `close`.
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
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLSpanElement>(null);
  useEffect(() => {
    if (!open) return undefined;
    const onPointer = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener('pointerdown', onPointer);
    return () => document.removeEventListener('pointerdown', onPointer);
  }, [open]);
  const close = () => setOpen(false);
  return (
    <span
      ref={root}
      className="relative inline-flex"
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
        aria-haspopup="menu"
        aria-expanded={open}
        className={fieldButtonClass}
        onClick={() => setOpen((was) => !was)}
      >
        {button}
        <span aria-hidden="true" className="text-ink-muted">
          ▾
        </span>
      </button>
      {open ? (
        <div
          data-testid={ids.menu}
          role="menu"
          className="absolute top-full left-0 z-30 mt-1 grid min-w-56 gap-px rounded-panel border border-edge bg-panel p-1 shadow-panel"
        >
          {children(close)}
        </div>
      ) : null}
    </span>
  );
}
