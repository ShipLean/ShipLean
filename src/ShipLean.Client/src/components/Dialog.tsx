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
  tall,
  testId,
  closeTestId,
  headerExtra,
  onBackdropClick,
}: {
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  /** A CSS length; the dialog is never wider than the screen. Defaults to `var(--dialog-width)`. */
  width?: string;
  /** Fills the screen's height instead of fitting its content. */
  tall?: boolean;
  /** Replaces `componentTestIds.dialog` / `dialogClose` for a dialog a test addresses on its own. */
  testId?: string;
  closeTestId?: string;
  /** Shown in the header, before the close button. */
  headerExtra?: ReactNode;
  /** A click on the backdrop (outside the panel). Without it the backdrop does nothing. */
  onBackdropClick?: () => void;
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
      onClick={(event) => {
        if (event.target === event.currentTarget) onBackdropClick?.();
      }}
      className="fixed inset-0 z-50 grid place-items-center p-4"
      style={{ background: 'color-mix(in srgb, var(--color-text) 45%, transparent)' }}
    >
      <div
        ref={panel}
        data-testid={testId ?? componentTestIds.dialog}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        onKeyDown={onKeyDown}
        style={{ width: `min(${width ?? 'var(--dialog-width)'}, 100%)` }}
        className={`flex max-h-[calc(100dvh-32px)] flex-col ${tall ? 'h-[calc(100dvh-32px)]' : ''}  overflow-hidden rounded-frame border border-edge bg-panel shadow-panel`}
      >
        <header className="flex items-start gap-2 border-b border-edge px-4 py-3">
          <h2
            id={titleId}
            className="m-0 flex-1 text-[length:var(--text-md)] font-semibold text-ink"
          >
            {title}
          </h2>
          {headerExtra}
          <button
            type="button"
            data-testid={closeTestId ?? componentTestIds.dialogClose}
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
