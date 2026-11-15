import { createPortal } from 'react-dom';
import { useId, useRef, type KeyboardEvent, type ReactNode } from 'react';
import { useModalFocus } from '../components/useModalFocus';
import { touchTestIds } from './testIds';
import { useSwipeToClose } from './useSwipeToClose';

type SheetProps = {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
};

/**
 * The bottom sheet (K5, SHIP-805 C4): grab bar, title on the left, Close on the right, a scrolling body and an optional
 * footer, over a scrim. Scrim tap, Esc, Close and a swipe down on the grab bar or head call `onClose`. Closed, it renders
 * nothing.
 */
export function Sheet({ open, ...panel }: SheetProps) {
  return open ? <SheetPanel {...panel} /> : null;
}

function SheetPanel({ title, onClose, children, footer }: Omit<SheetProps, 'open'>) {
  const sheet = useRef<HTMLDivElement>(null);
  const titleId = useId();
  // Focuses the sheet itself, not its first field: a search field stays unfocused (no keyboard) until tapped (C4).
  useModalFocus(sheet);
  const { offset, handlers } = useSwipeToClose(onClose);

  // A field inside that used Esc itself marks the event handled. The Esc the sheet handles is marked too: React events
  // bubble through portals, and a Dialog the sheet was opened from closes on any Esc that is not defaultPrevented.
  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape' && !event.defaultPrevented) {
      event.preventDefault();
      onClose();
    }
  };

  return createPortal(
    <>
      <div
        data-testid={touchTestIds.sheetScrim}
        aria-hidden="true"
        className="fixed inset-0 z-[65]"
        style={{ background: 'var(--scrim-touch)' }}
        // Keep focus in the sheet: a press on the scrim would move it to the body.
        onMouseDown={(event) => event.preventDefault()}
        onClick={onClose}
      />
      <div
        ref={sheet}
        data-testid={touchTestIds.sheet}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        onKeyDown={onKeyDown}
        className={`fixed inset-x-0 bottom-0 z-[70] flex max-h-[85dvh] flex-col rounded-t-frame border-t border-edge bg-panel pb-[env(safe-area-inset-bottom)] shadow-[0_-12px_32px_rgb(0_0_0/0.22)] outline-none ${offset === 0 ? 'transition-transform duration-150' : ''}`}
        style={{ transform: offset > 0 ? `translateY(${offset}px)` : undefined }}
      >
        <div className="flex-none touch-none" {...handlers}>
          <div
            data-testid={touchTestIds.sheetGrab}
            aria-hidden="true"
            className="mx-auto mt-1.5 h-1 w-9 rounded-sm bg-edge"
          />
          <div className="flex items-center gap-2 py-1 pr-1 pl-3">
            <h2
              id={titleId}
              data-testid={touchTestIds.sheetTitle}
              className="m-0 min-w-0 flex-1 truncate text-[length:var(--text-md)] font-semibold text-ink"
            >
              {title}
            </h2>
            <button
              type="button"
              data-testid={touchTestIds.sheetClose}
              className="min-h-[var(--touch-target)] rounded-control px-3 text-[length:var(--text-base)] font-medium text-signal"
              onClick={onClose}
            >
              Close
            </button>
          </div>
        </div>
        <div
          data-testid={touchTestIds.sheetBody}
          className="grid min-h-0 flex-1 content-start gap-1 overflow-y-auto overscroll-contain px-3 pb-3"
        >
          {children}
        </div>
        {footer ? (
          <div
            data-testid={touchTestIds.sheetFooter}
            className="flex flex-none gap-2 border-t border-edge px-3 py-2"
          >
            {footer}
          </div>
        ) : null}
      </div>
    </>,
    document.body,
  );
}
