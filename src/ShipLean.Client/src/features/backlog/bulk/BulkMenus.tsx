import { useEffect, useRef, useState, type ReactNode } from 'react';
import { backlogTestIds as ids } from '../testIds';

/**
 * A bar button that opens a panel above it. Esc and a click outside close it, and focus goes back to the button. The
 * panel's content gets `close`.
 */
export function BulkMenu({
  testId,
  label,
  disabled,
  children,
}: {
  testId: string;
  label: string;
  disabled: boolean;
  children: (close: () => void) => ReactNode;
}) {
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLSpanElement>(null);
  const button = useRef<HTMLButtonElement>(null);
  const wasOpen = useRef(false);
  useEffect(() => {
    if (!open) return undefined;
    const onPointer = (event: PointerEvent) => {
      if (root.current?.contains(event.target as Node)) return;
      wasOpen.current = false;
      setOpen(false);
    };
    document.addEventListener('pointerdown', onPointer);
    return () => document.removeEventListener('pointerdown', onPointer);
  }, [open]);
  // Focus goes back to the button when the menu closes, however it closed.
  useEffect(() => {
    if (!open && wasOpen.current) button.current?.focus();
    wasOpen.current = open;
  }, [open]);
  const close = () => setOpen(false);
  return (
    <span
      ref={root}
      className="relative inline-flex"
      onKeyDown={(event) => {
        if (event.key === 'Escape' && open) {
          event.stopPropagation();
          close();
        }
      }}
    >
      <button
        ref={button}
        type="button"
        data-testid={testId}
        disabled={disabled}
        aria-haspopup="menu"
        aria-expanded={open}
        className="btn-ghost disabled:opacity-50"
        onClick={() => setOpen((was) => !was)}
      >
        {label}
        <span aria-hidden="true" className="ml-1 text-ink-muted">
          ▴
        </span>
      </button>
      {open ? (
        <div
          data-testid={ids.bulkMenu}
          role="menu"
          aria-label={label}
          className="absolute bottom-full left-0 z-20 mb-1.5 grid max-h-[280px] min-w-48 gap-px overflow-auto rounded-panel border border-edge bg-panel p-1 shadow-panel"
        >
          {children(close)}
        </div>
      ) : null}
    </span>
  );
}

export const menuOptionClass =
  'flex h-[26px] items-center gap-1.5 rounded-control px-2 text-left text-[length:var(--text-sm)] whitespace-nowrap text-ink hover:bg-signal-soft';
