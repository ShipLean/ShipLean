import { Sheet } from '../../../touch/Sheet';
import { sprintsTestIds as ids } from '../testIds';
import type { CapacityLine } from './CapacityRow';
import { effectiveDaysOf, type CapacityShape, type DraftRow } from './capacityDraft';

interface CapacityDaysSheetProps {
  /** The person being edited; null closes the sheet. */
  line: CapacityLine | null;
  shape: CapacityShape;
  sprintName: string;
  /** The velocity the points are worked from; undefined while the box holds no valid number. */
  velocity: number | undefined;
  /** How many people share the velocity. */
  people: number;
  onChange: (row: DraftRow) => void;
  onRemove: () => void;
  onClose: () => void;
}

const label = 'm-0 text-[length:var(--text-sm)] font-semibold text-ink-muted';
const step =
  'inline-flex size-10 flex-none items-center justify-center rounded-control border border-edge bg-panel text-[length:var(--text-md)] text-ink active:bg-signal-soft disabled:opacity-50';

/**
 * A person's days on a phone (SHIP-1808 P3): − / + by half a day, Use default, their capacity with its sum, the note
 * and Remove. Every change goes into the draft at once; the Save bar saves it.
 */
export function CapacityDaysSheet({
  line,
  shape,
  sprintName,
  velocity,
  people,
  onChange,
  onRemove,
  onClose,
}: CapacityDaysSheetProps) {
  if (line === null) return null;
  const { user, row, points } = line;
  const isDefault = row.days === '';
  const days = effectiveDaysOf(row, shape) ?? shape.defaultDays;
  const stepTo = (value: number) => onChange({ ...row, days: `${value}` });
  return (
    <Sheet open title={user.name} onClose={onClose}>
      <div className="grid gap-3 pt-1">
        <div className="grid gap-1.5">
          <p className={label}>Days available</p>
          <div className="flex items-center gap-2">
            <button
              type="button"
              data-testid={ids.capacityDaysLess}
              className={step}
              aria-label={`Half a day less for ${user.name}`}
              disabled={days <= 0}
              onClick={() => stepTo(Math.max(0, days - 0.5))}
            >
              −
            </button>
            <input
              data-testid={ids.capacityDays}
              className="field w-[4.5rem] text-center tabular-nums"
              type="number"
              min={0}
              max={shape.workingDays}
              step={0.5}
              inputMode="decimal"
              aria-label={`Days available for ${user.name}`}
              placeholder={`${shape.defaultDays}`}
              value={row.days}
              onChange={(event) => onChange({ ...row, days: event.target.value })}
            />
            <button
              type="button"
              data-testid={ids.capacityDaysMore}
              className={step}
              aria-label={`Half a day more for ${user.name}`}
              disabled={days >= shape.workingDays}
              onClick={() => stepTo(Math.min(shape.workingDays, days + 0.5))}
            >
              +
            </button>
            <span className="text-[length:var(--text-sm)] text-ink-muted">
              of {shape.workingDays} working days
            </span>
          </div>
          <div className="flex min-h-[var(--touch-target)] items-center justify-between gap-2 text-[length:var(--text-xs)] text-ink-muted">
            <span>Default {shape.defaultDays} days</span>
            {isDefault ? (
              <span data-testid={ids.capacityDaysDefault}>default</span>
            ) : (
              <button
                type="button"
                data-testid={ids.capacityDaysReset}
                className="min-h-[var(--touch-target)] rounded-control px-1 text-[length:var(--text-sm)] font-medium text-signal"
                aria-label={`Reset days for ${user.name} to the default`}
                onClick={() => onChange({ ...row, days: '' })}
              >
                Use default
              </button>
            )}
          </div>
        </div>
        <div className="grid gap-0.5 rounded-control bg-sunken px-3 py-2">
          <div className="flex items-baseline justify-between gap-2">
            <span>Capacity</span>
            <span className="font-semibold tabular-nums">
              <span data-testid={ids.capacitySheetPoints}>{points ?? '—'}</span> pts
            </span>
          </div>
          {velocity === undefined ? null : (
            <span className="text-[length:var(--text-xs)] text-ink-muted">
              {velocity} velocity ÷ {people} {people === 1 ? 'person' : 'people'} × {days} ÷{' '}
              {shape.workingDays} days
            </span>
          )}
        </div>
        <label className="grid gap-1.5">
          <span className={label}>Note</span>
          <input
            data-testid={ids.capacityNote}
            className="field w-full"
            placeholder="Optional, e.g. Off Thu–Fri"
            aria-label={`Note for ${user.name}`}
            value={row.note}
            onChange={(event) => onChange({ ...row, note: event.target.value })}
          />
        </label>
        <div className="border-t border-edge pt-1">
          <button
            type="button"
            data-testid={ids.capacityRemove}
            className="grid min-h-[var(--touch-target)] w-full content-center rounded-control py-1 text-left active:bg-signal-soft"
            onClick={() => {
              onRemove();
              onClose();
            }}
          >
            <span className="text-alert">Remove from {sprintName}</span>
            <span className="text-[length:var(--text-xs)] text-ink-muted">
              Their share goes to everyone else in the table
            </span>
          </button>
        </div>
      </div>
    </Sheet>
  );
}
