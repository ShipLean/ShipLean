import { createPortal } from 'react-dom';
import { useId, useRef, type KeyboardEvent, type MouseEvent, type ReactNode } from 'react';
import { touchTestIds } from '../touch/testIds';
import { useTouchShell } from '../touch/useTouchShell';
import { componentTestIds } from './testIds';
import { useModalFocus } from './useModalFocus';

/**
 * A modal. Focus moves in and is trapped, Esc and the close button call `onClose`, focus returns to the opener.
 * At 900px and below (K5, SHIP-805 C9) it fills the screen: a ✕ at the top left, `primaryAction` at the top right, and
 * the body scrolls between the header and the footer. Above 900px it renders as it always has.
 */
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
  titleIcon,
  subtitle,
  onBackdropClick,
  primaryAction,
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
  /** Shown in the header before the title. */
  titleIcon?: ReactNode;
  /**
   * A second header line under the icon and title (for example "Result 2 of 9 · Sprint 4"). Opt-in: with it the icon and
   * the title share line 1 and the title truncates; the dialog's name is then the whole of line 1 and the subtitle is
   * its description. Without it the header renders as it always has.
   */
  subtitle?: string;
  /** A click on the backdrop (outside the panel). Without it the backdrop does nothing. */
  onBackdropClick?: () => void;
  /** The header's main action at the top right, shown only on the full-screen touch layout. */
  primaryAction?: ReactNode;
}) {
  const { touch } = useTouchShell();
  const panel = useRef<HTMLDivElement>(null);
  const titleId = useId();
  const headlineId = `${titleId}-headline`;
  const subtitleId = `${titleId}-subtitle`;
  useModalFocus(panel);

  // A click on the backdrop would move focus to the body, outside the trap; keep it where it is.
  // A drag that starts in the panel (selecting text) and ends on the backdrop is not a click on the backdrop.
  const pressedBackdrop = useRef(false);
  const keepFocus = (event: MouseEvent) => {
    pressedBackdrop.current = event.target === event.currentTarget;
    if (pressedBackdrop.current) event.preventDefault();
  };

  // A field inside that used Esc itself (closing a mention list, cancelling an add form) marks the event handled.
  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape' && !event.defaultPrevented) {
      event.stopPropagation();
      onClose();
    }
  };

  const backdropProps = {
    onMouseDown: keepFocus,
    onClick: (event: MouseEvent) => {
      if (event.target === event.currentTarget && pressedBackdrop.current) onBackdropClick?.();
    },
    style: { background: 'color-mix(in srgb, var(--color-text) 45%, transparent)' },
  };
  const panelProps = {
    ref: panel,
    'data-testid': testId ?? componentTestIds.dialog,
    role: 'dialog',
    'aria-modal': true,
    'aria-labelledby': subtitle ? headlineId : titleId,
    'aria-describedby': subtitle ? subtitleId : undefined,
    tabIndex: -1,
    onKeyDown,
  } as const;
  const heading = (
    <h2 id={titleId} className="m-0 flex-1 text-[length:var(--text-md)] font-semibold text-ink">
      {title}
    </h2>
  );

  // With a subtitle: [icon + truncating title] on line 1, the subtitle on line 2 starting under the icon.
  const headline = subtitle ? (
    <div className="flex min-w-0 flex-1 flex-col">
      <div id={headlineId} className="flex min-w-0 items-center gap-1.5">
        {titleIcon}
        <h2
          id={titleId}
          data-testid={componentTestIds.dialogTitle}
          className="m-0 min-w-0 flex-1 truncate text-[length:var(--text-md)] font-semibold text-ink"
        >
          {title}
        </h2>
      </div>
      <p
        id={subtitleId}
        data-testid={componentTestIds.dialogSubtitle}
        className="m-0 text-[length:var(--text-sm)] text-ink-muted"
      >
        {subtitle}
      </p>
    </div>
  ) : (
    <>
      {titleIcon}
      {heading}
    </>
  );

  if (touch) {
    // One Close (the ✕ first in the header) and no inline width: a style attribute would beat any class.
    return createPortal(
      <div {...backdropProps} className="fixed inset-0 z-50 grid place-items-center">
        <div
          {...panelProps}
          className="flex h-[100dvh] w-[100dvw] flex-col overflow-hidden bg-panel"
        >
          <header className="flex items-center gap-2 border-b border-edge px-2 pt-[calc(env(safe-area-inset-top)+0.5rem)] pb-2">
            <button
              type="button"
              data-testid={touchTestIds.dialogClose}
              aria-label="Close"
              className="grid size-[var(--touch-target)] shrink-0 place-items-center rounded-control text-ink-muted"
              onClick={onClose}
            >
              ✕
            </button>
            {headline}
            {headerExtra}
            {primaryAction ? (
              <div data-testid={touchTestIds.dialogPrimary} className="flex shrink-0 items-center">
                {primaryAction}
              </div>
            ) : null}
          </header>
          <div className="grid min-h-0 flex-1 auto-rows-max content-start gap-3 overflow-auto px-4 py-3">
            {children}
          </div>
          {footer ? (
            <footer className="flex items-center gap-2 border-t border-edge bg-canvas px-4 pt-2 pb-[calc(env(safe-area-inset-bottom)+0.5rem)]">
              {footer}
            </footer>
          ) : null}
        </div>
      </div>,
      document.body,
    );
  }

  return createPortal(
    <div {...backdropProps} className="fixed inset-0 z-50 grid place-items-center p-4">
      <div
        {...panelProps}
        style={{ width: `min(${width ?? 'var(--dialog-width)'}, 100%)` }}
        className={`flex max-h-[calc(100dvh-32px)] flex-col ${tall ? 'h-[calc(100dvh-32px)]' : ''}  overflow-hidden rounded-frame border border-edge bg-panel shadow-panel`}
      >
        <header className="flex items-start gap-2 border-b border-edge px-4 py-3">
          {headline}
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
