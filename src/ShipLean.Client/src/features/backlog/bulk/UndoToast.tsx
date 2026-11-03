import { useEffect } from 'react';
import { backlogTestIds as ids } from '../testIds';

export const UNDO_MS = 9000;

/** The toast that says what a bulk run did, with Undo for about nine seconds. Sits above the bar while rows are selected. */
export function UndoToast({
  message,
  aboveBar,
  onUndo,
  onExpire,
}: {
  message: string;
  aboveBar: boolean;
  onUndo: () => void;
  onExpire: () => void;
}) {
  useEffect(() => {
    const timer = setTimeout(onExpire, UNDO_MS);
    return () => clearTimeout(timer);
  }, [onExpire]);

  return (
    <div
      role="status"
      className={`fixed left-1/2 z-40 flex -translate-x-1/2 items-center gap-3 whitespace-nowrap rounded-panel bg-ink py-[7px] pl-3.5 pr-2 text-[length:var(--text-sm)] font-medium text-canvas shadow-panel ${aboveBar ? 'bottom-24' : 'bottom-5'}`}
    >
      <span>{message}</span>
      <button
        type="button"
        data-testid={ids.undo}
        className="h-6 rounded-control border border-canvas/45 px-2 text-[length:var(--text-xs)] font-semibold hover:bg-canvas/20"
        onClick={onUndo}
      >
        Undo
      </button>
    </div>
  );
}
