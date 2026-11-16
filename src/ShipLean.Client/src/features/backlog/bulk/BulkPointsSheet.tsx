import { useState } from 'react';
import { Sheet } from '../../../touch/Sheet';
import { backlogTestIds as ids } from '../testIds';

const buttonClass =
  'min-h-[var(--touch-target)] rounded-control border border-edge px-4 text-[length:var(--text-base)] font-medium disabled:text-ink-muted';

/** The bar's Points sheet: a 16px number field, Set for a number and Clear to remove the estimate from every selected row. */
export function BulkPointsSheet({
  onSet,
  onClose,
}: {
  onSet: (points: number | null) => void;
  onClose: () => void;
}) {
  const [text, setText] = useState('');
  const trimmed = text.trim();
  const points = trimmed === '' ? null : Number(trimmed);
  const valid = points !== null && Number.isFinite(points) && points >= 0;
  const set = (value: number | null) => {
    onSet(value);
    onClose();
  };
  return (
    <Sheet open title="Points" onClose={onClose}>
      <div className="grid gap-2">
        <input
          type="number"
          min={0}
          step="any"
          inputMode="decimal"
          data-testid={ids.bulkSheetPointsInput}
          aria-label="Points"
          placeholder="Points"
          value={text}
          className="h-11 w-full rounded-control border border-edge bg-panel px-3 text-[length:var(--text-base)] tabular-nums"
          onChange={(event) => setText(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' && valid) {
              event.preventDefault();
              set(points);
            }
          }}
        />
        <div className="flex gap-2 pt-1">
          <button
            type="button"
            data-testid={ids.bulkSheetPointsClear}
            className={buttonClass}
            onClick={() => set(null)}
          >
            Clear
          </button>
          <button
            type="button"
            data-testid={ids.bulkSheetPointsSet}
            disabled={!valid}
            className={`${buttonClass} flex-1 border-signal bg-signal text-signal-ink disabled:border-edge disabled:bg-transparent`}
            onClick={() => set(points)}
          >
            Set
          </button>
        </div>
      </div>
    </Sheet>
  );
}
