import { useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';
import { useTouchSelectMode } from './selectMode';

const buttonClass =
  'inline-flex min-h-[var(--touch-target)] items-center rounded-control px-3 text-[length:var(--text-base)] font-medium text-signal active:bg-signal-soft disabled:text-ink-muted';

/**
 * Under the toolbar while the touch Select mode is on (SHIP-716 D3): Select all (or Clear when every shown row is
 * selected), how many are selected, and Done, which clears the selection and leaves the mode.
 */
export function SelectBar() {
  const { selecting, stop } = useTouchSelectMode();
  const selection = useSelection();
  if (!selecting) return null;
  const shown = selection.orderedIds;
  const all = shown.length > 0 && shown.every((id) => selection.has(id));
  return (
    <div
      data-testid={ids.selectBar}
      className="flex flex-none items-center gap-1 border-b border-edge bg-panel px-1"
    >
      <button
        type="button"
        data-testid={ids.selectBarAll}
        disabled={shown.length === 0}
        className={buttonClass}
        onClick={() => (all ? selection.removeMany(shown) : selection.addMany(shown))}
      >
        {all ? 'Clear all' : 'Select all'}
      </button>
      <span
        data-testid={ids.selectBarCount}
        className="min-w-0 flex-1 truncate text-center text-[length:var(--text-sm)] text-ink-muted"
      >
        {selection.count === 0 ? 'Select rows' : `${selection.count} selected`}
      </span>
      <button
        type="button"
        data-testid={ids.selectBarDone}
        className={`${buttonClass} font-semibold`}
        onClick={() => {
          selection.clear();
          stop();
        }}
      >
        Done
      </button>
    </div>
  );
}
