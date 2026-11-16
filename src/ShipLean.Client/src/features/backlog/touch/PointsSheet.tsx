import { useState } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { ConflictBanner } from '../../../components/ConflictBanner';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { Sheet } from '../../../touch/Sheet';
import { parsePoints, type Estimate } from '../estimate/useEstimate';
import { ParentCell } from '../list/ParentCell';
import { backlogTestIds as ids } from '../testIds';

/** The scale the sheet offers as one-tap buttons; ? clears the points. Other values go in the field. */
const SCALE = [1, 2, 3, 5, 8, 13, 21] as const;

const buttonClass =
  'min-h-[var(--touch-target)] rounded-control border border-edge bg-panel px-4 text-[length:var(--text-base)] font-medium disabled:text-ink-muted';

/** Where the sheet is in the not-estimated queue, and how to leave the item. */
export interface PointsQueueStep {
  position: number;
  total: number;
  /** Leave the item as it is and go to the next. */
  onSkip: () => void;
}

function QueueProgress({ position, total }: { position: number; total: number }) {
  return (
    <div
      data-testid={ids.queuePosition}
      className="flex items-center gap-3 text-[length:var(--text-sm)] text-ink-muted"
    >
      <span>Not estimated</span>
      <span aria-hidden className="flex flex-1 gap-1">
        {Array.from({ length: total }, (_, index) => (
          <span
            key={index}
            className={`h-1 max-w-8 flex-1 rounded-full ${index < position ? 'bg-signal' : 'bg-edge'}`}
          />
        ))}
      </span>
      <span className="tabular-nums">
        {position} of {total}
      </span>
    </div>
  );
}

function ItemCard({ row }: { row: BacklogRowDto }) {
  return (
    <div className="grid gap-1 rounded-panel border border-edge bg-sunken px-3 py-2">
      <div className="flex items-start gap-1.5">
        {row.type ? (
          <span className="mt-0.5 flex-none">
            <TypeIcon type={row.type.id} />
          </span>
        ) : null}
        <span className="line-clamp-2 min-w-0 font-medium break-words">{row.title}</span>
      </div>
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-[length:var(--text-xs)] text-ink-muted">
        <span data-testid={ids.queueStepKey}>{row.key}</span>
        {row.state ? <StateDot category={row.state.category} name={row.state.name} /> : null}
        {row.parent ? (
          <span className="max-w-full min-w-0 break-words">
            <ParentCell parent={row.parent} />
          </span>
        ) : null}
      </div>
    </div>
  );
}

/**
 * The Points sheet's body: the item, the 1–21 scale and ? as one-tap buttons, and a field for any other value. A pick
 * saves; `onSaved` runs once the server took it. A save that fails keeps the item and says so.
 */
export function PointsBody({
  row,
  estimate,
  queue,
  onSaved,
}: {
  row: BacklogRowDto;
  estimate: Estimate;
  queue?: PointsQueueStep;
  onSaved: () => void;
}) {
  const [text, setText] = useState(estimate.shown === null ? '' : String(estimate.shown));
  const [invalid, setInvalid] = useState(false);
  const [state, setState] = useState<'idle' | 'saving' | 'failed'>('idle');
  const save = async (value: number | null) => {
    setState('saving');
    if (await estimate.commit(value, false)) onSaved();
    else setState('failed');
  };
  const saveText = () => {
    const parsed = parsePoints(text);
    if (!parsed.ok) {
      setInvalid(true);
      return;
    }
    void save(parsed.value);
  };
  const off = !estimate.editable || state === 'saving';
  return (
    <>
      {queue ? <QueueProgress position={queue.position} total={queue.total} /> : null}
      <ItemCard row={row} />
      {estimate.conflict !== null ? (
        <ConflictBanner
          changedBy={`${estimate.conflict.changedBy} (${row.key})`}
          onUseMine={estimate.useMine}
          onKeepTheirs={estimate.keepTheirs}
        />
      ) : (
        <>
          <div className="grid grid-cols-4 gap-2">
            {SCALE.map((points) => (
              <button
                key={points}
                type="button"
                data-testid={ids.pointsScale}
                data-points={points}
                aria-pressed={estimate.shown === points}
                disabled={off}
                className={`${buttonClass} tabular-nums aria-pressed:border-signal aria-pressed:bg-signal-soft`}
                onClick={() => void save(points)}
              >
                {points}
              </button>
            ))}
            <button
              type="button"
              data-testid={ids.pointsSheetClear}
              aria-label="Not estimated"
              disabled={off}
              className={buttonClass}
              onClick={() => void save(null)}
            >
              ?
            </button>
          </div>
          <div className="flex gap-2">
            <input
              data-testid={ids.pointsSheetInput}
              aria-label={`Other points for ${row.key}`}
              aria-invalid={invalid}
              inputMode="decimal"
              value={text}
              placeholder="Other value"
              className="h-[var(--touch-target)] min-w-0 flex-1 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-base)] tabular-nums aria-invalid:border-alert"
              onChange={(event) => {
                setText(event.target.value);
                setInvalid(false);
              }}
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  event.preventDefault();
                  saveText();
                }
              }}
            />
            <button
              type="button"
              data-testid={ids.pointsSheetSave}
              disabled={off}
              className={buttonClass}
              onClick={saveText}
            >
              Save
            </button>
          </div>
          {invalid ? (
            <p role="alert" className="m-0 text-[length:var(--text-sm)] text-alert">
              Use a number from 0 to 9999.99, with up to two decimals.
            </p>
          ) : null}
          {state === 'failed' ? (
            <p
              data-testid={ids.pointsSheetError}
              role="alert"
              className="m-0 text-[length:var(--text-sm)] text-alert"
            >
              {row.key} wasn&apos;t saved. Pick again{queue ? ' or skip it' : ''}.
            </p>
          ) : null}
        </>
      )}
      <div className="flex items-center gap-2 pt-1">
        <span className="min-w-0 flex-1 text-[length:var(--text-sm)] text-ink-muted">
          {queue ? 'Picking one saves it and goes to the next.' : 'Picking one saves it.'}
        </span>
        {queue ? (
          <button
            type="button"
            data-testid={ids.queueSkip}
            className={buttonClass}
            onClick={queue.onSkip}
          >
            Skip
          </button>
        ) : null}
      </div>
    </>
  );
}

/** The Points sheet (SHIP-716 D9) for the ? chip and the row's ⋯: a pick saves and closes. */
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
      <div data-testid={ids.pointsSheet} className="grid gap-3">
        <PointsBody row={row} estimate={estimate} onSaved={onClose} />
      </div>
    </Sheet>
  );
}
