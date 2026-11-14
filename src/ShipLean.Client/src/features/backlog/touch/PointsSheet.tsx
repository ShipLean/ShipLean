import { useState } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { ConflictBanner } from '../../../components/ConflictBanner';
import { Sheet } from '../../../touch/Sheet';
import { parsePoints, type Estimate } from '../estimate/useEstimate';
import { backlogTestIds as ids } from '../testIds';

const buttonClass =
  'min-h-[var(--touch-target)] rounded-control border border-edge px-4 text-[length:var(--text-base)] font-medium disabled:text-ink-muted';

function Body({
  row,
  estimate,
  onClose,
}: {
  row: BacklogRowDto;
  estimate: Estimate;
  onClose: () => void;
}) {
  const [text, setText] = useState(estimate.shown === null ? '' : String(estimate.shown));
  const [invalid, setInvalid] = useState(false);
  const save = (next: string) => {
    const parsed = parsePoints(next);
    if (!parsed.ok) {
      setInvalid(true);
      return;
    }
    estimate.commit(parsed.value, false);
    onClose();
  };
  if (estimate.conflict !== null) {
    return (
      <ConflictBanner
        changedBy={`${estimate.conflict.changedBy} (${row.key})`}
        onUseMine={estimate.useMine}
        onKeepTheirs={estimate.keepTheirs}
      />
    );
  }
  return (
    <>
      <input
        data-testid={ids.pointsSheetInput}
        aria-label={`Points for ${row.key}`}
        aria-invalid={invalid}
        inputMode="decimal"
        value={text}
        placeholder="Not estimated"
        className="h-11 w-full rounded-control border border-edge bg-panel px-3 text-[length:var(--text-base)] tabular-nums aria-invalid:border-alert"
        onChange={(event) => {
          setText(event.target.value);
          setInvalid(false);
        }}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            save(text);
          }
        }}
      />
      {invalid ? (
        <p role="alert" className="m-0 text-[length:var(--text-sm)] text-alert">
          Use a number from 0 to 9999.99, with up to two decimals.
        </p>
      ) : null}
      <div className="flex gap-2 pt-1">
        <button
          type="button"
          data-testid={ids.pointsSheetClear}
          className={buttonClass}
          disabled={estimate.shown === null}
          onClick={() => save('')}
        >
          Clear
        </button>
        <button
          type="button"
          data-testid={ids.pointsSheetSave}
          className={`${buttonClass} flex-1 border-signal bg-signal text-signal-ink`}
          onClick={() => save(text)}
        >
          Save
        </button>
      </div>
    </>
  );
}

/** The Points sheet (SHIP-716 D9): a 16px number field with Clear and Save, for the ? chip, the row's ⋯ and the not-estimated queue. */
export function PointsSheet({
  row,
  estimate,
  open,
  onClose,
}: {
  row: BacklogRowDto;
  estimate: Estimate;
  open: boolean;
  onClose: () => void;
}) {
  return (
    <Sheet open={open} title={`Points · ${row.key}`} onClose={onClose}>
      <div data-testid={ids.pointsSheet} className="grid gap-2">
        <Body row={row} estimate={estimate} onClose={onClose} />
      </div>
    </Sheet>
  );
}
