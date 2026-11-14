import type { ReactNode } from 'react';
import { touchTestIds } from './testIds';
import { useTouchShell } from './useTouchShell';

// One line of controls plus the bottom safe area; the spacer takes the same height so nothing ends up under the bar.
const BAR_HEIGHT = 'h-[calc(var(--touch-target)+1rem+env(safe-area-inset-bottom))]';

/** The room the bar covers at the end of the page: one line of controls plus the bottom safe area. */
export function SaveBarSpacer() {
  const { touch } = useTouchShell();
  if (!touch) return null;
  return <div aria-hidden="true" className={`${BAR_HEIGHT} shrink-0`} />;
}

/**
 * The touch Save bar (K5, SHIP-805 rule 15): message · Discard · Save, fixed to the bottom while something is unsaved.
 * It renders only at 900px and below; above that, pages keep their own Save and Cancel. Put it last in the scrolling
 * content: it leaves an in-flow spacer of its own height so the end of the page can scroll clear of it. A page whose bar
 * is not at its end passes `spacer={false}` and renders a `SaveBarSpacer` last instead.
 */
export function SaveBar({
  dirty,
  message,
  onDiscard,
  onSave,
  saveDisabled = false,
  saving = false,
  spacer = true,
}: {
  dirty: boolean;
  /** Text, or an element such as a button that opens the list of changes. */
  message: ReactNode;
  onDiscard: () => void;
  onSave: () => void;
  saveDisabled?: boolean;
  saving?: boolean;
  /** Leave the in-flow spacer to the caller (`SaveBarSpacer`). Default true. */
  spacer?: boolean;
}) {
  const { touch } = useTouchShell();
  if (!touch || !dirty) return null;

  return (
    <>
      {spacer ? <SaveBarSpacer /> : null}
      <div
        data-testid={touchTestIds.saveBar}
        role="region"
        aria-label="Unsaved changes"
        className={`fixed inset-x-0 bottom-0 z-40 flex ${BAR_HEIGHT} items-start gap-2 border-t border-edge bg-panel px-3 pt-2 pb-[env(safe-area-inset-bottom)]`}
      >
        <div
          data-testid={touchTestIds.saveBarMessage}
          className="min-w-0 flex-1 truncate text-[length:var(--text-sm)] leading-[var(--touch-target)] text-ink-muted [&>*]:min-h-[var(--touch-target)]"
        >
          {message}
        </div>
        <button
          type="button"
          data-testid={touchTestIds.saveBarDiscard}
          className="btn-ghost h-[var(--touch-target)] shrink-0"
          onClick={onDiscard}
        >
          Discard
        </button>
        <button
          type="button"
          data-testid={touchTestIds.saveBarSave}
          className="btn-signal h-[var(--touch-target)] shrink-0 disabled:opacity-50"
          disabled={saveDisabled || saving}
          onClick={onSave}
        >
          {saving ? 'Saving…' : 'Save'}
        </button>
      </div>
    </>
  );
}
