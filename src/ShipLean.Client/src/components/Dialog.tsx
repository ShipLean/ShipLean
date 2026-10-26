import { createPortal } from 'react-dom';
import { useId, useRef, type KeyboardEvent, type MouseEvent, type ReactNode } from 'react';
import { componentTestIds } from './testIds';
import { useModalFocus } from './useModalFocus';

/** A modal. Focus moves in and is trapped, Esc and the close button call `onClose`, focus returns to the opener. */
export function Dialog({
  title,
  onClose,
  children,
  footer,
  width,
}: {
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  /** A CSS length; the dialog is never wider than the screen. Defaults to `var(--dialog-width)`. */
  width?: string;
}) {
  const panel = useRef<HTMLDivElement>(null);
  const titleId = useId();
  useModalFocus(panel);

  // A click on the backdrop would move focus to the body, outside the trap; keep it where it is.
  const keepFocus = (event: MouseEvent) => {
    if (event.target === event.currentTarget) event.preventDefault();
  };

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape') {
      event.stopPropagation();
      onClose();
    }
  };

  return createPortal(
    <div
      onMouseDown={keepFocus}
      className="fixed inset-0 z-50 grid place-items-center p-4"
      style={{ background: 'color-mix(in srgb, var(--color-text) 45%, transparent)' }}
    >
      <div
        ref={panel}
        data-testid={componentTestIds.dialog}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        onKeyDown={onKeyDown}
        style={{ width: `min(${width ?? 'var(--dialog-width)'}, 100%)` }}
        className="flex max-h-[calc(100dvh-32px)] flex-col overflow-hidden rounded-frame border border-edge bg-panel shadow-panel"
      >
        <header className="flex items-start gap-2 border-b border-edge px-4 py-3">
          <h2
            id={titleId}
            className="m-0 flex-1 text-[length:var(--text-md)] font-semibold text-ink"
          >
            {title}
          </h2>
          <button
            type="button"
            data-testid={componentTestIds.dialogClose}
            aria-label="Close"
            className="text-ink-muted hover:text-ink"
            onClick={onClose}
          >
            ×
          </button>
        </header>
        <div className="grid gap-3 overflow-auto px-4 py-3">{children}</div>
        {footer ? (
          <footer className="flex items-center gap-2 border-t border-edge bg-canvas px-4 py-2">
            {footer}
          </footer>
        ) : null}
      </div>
    </div>,
    document.body,
  );
}
