import { CapacityBar } from '../../../components/CapacityBar';
import { backlogTestIds as ids } from '../testIds';
import type { SprintSum } from './sprintSum';

/** Move to a sprint, with what the sprint would hold: it warns when over and never blocks. */
export function SprintMoveButton({
  name,
  current,
  sum,
  hint,
  disabled,
  onMove,
}: {
  name: string;
  current: boolean;
  sum: SprintSum;
  /** The digit that picks this sprint while the move keys are waiting; absent otherwise. */
  hint: number | undefined;
  disabled: boolean;
  onMove: () => void;
}) {
  const overBy = sum.of === null ? 0 : sum.after - sum.of;
  return (
    <button
      type="button"
      data-testid={ids.bulkMoveSprint}
      disabled={disabled}
      title={`Move the selection to ${name}`}
      className="grid min-w-28 gap-0.5 rounded-control border border-edge bg-panel px-2 py-[3px] text-left hover:border-signal hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-signal disabled:opacity-50"
      onClick={onMove}
    >
      <span className="flex items-center gap-1.5 text-[length:var(--text-xs)] font-semibold whitespace-nowrap text-ink">
        {hint === undefined ? null : (
          <kbd className="inline-grid h-[18px] min-w-[18px] place-items-center rounded-sm border border-edge px-1 font-mono text-[length:var(--text-2xs)]">
            {hint}
          </kbd>
        )}
        {name}
        {current ? <span className="font-normal text-ink-muted">current</span> : null}
      </span>
      {sum.of === null ? null : (
        <CapacityBar used={sum.after} capacity={sum.of} variant="capacity" />
      )}
      <span
        data-testid={ids.bulkSum}
        data-over={sum.over}
        className={`text-[length:var(--text-2xs)] whitespace-nowrap tabular-nums ${sum.over ? 'font-semibold text-alert' : 'text-ink-muted'}`}
      >
        {sum.text}
        {sum.over ? ` · +${overBy} over` : ''}
      </span>
    </button>
  );
}
