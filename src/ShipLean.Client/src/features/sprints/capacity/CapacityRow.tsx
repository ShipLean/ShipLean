import type { UserRef } from '@shiplean/core/api/refs';
import { Avatar } from '../../../components/Avatar';
import { sprintsTestIds as ids } from '../testIds';
import type { CapacityShape, DraftRow } from './capacityDraft';

/** One line of the table: who, and what the sprint holds for them. */
export interface CapacityLine {
  user: UserRef;
  row: DraftRow;
  /** Their capacity; undefined while the velocity or their days cannot be read. */
  points: number | undefined;
}

interface CapacityRowProps {
  line: CapacityLine;
  shape: CapacityShape;
  /** An ended sprint: stored values as text, nothing to edit. */
  readOnly: boolean;
  onChange: (row: DraftRow) => void;
  onRemove: () => void;
}

export const cell = 'px-3 py-1.5 align-middle touch:block touch:p-0';
const small = 'text-[length:var(--text-xs)] text-ink-muted';

/** One member: who, their days (the default until typed over), the capacity those days give and a note. */
export function CapacityRow({ line, shape, readOnly, onChange, onRemove }: CapacityRowProps) {
  const { user, row, points } = line;
  const isDefault = row.days === '';
  return (
    <tr
      data-testid={ids.capacityRow}
      className="border-t border-edge touch:grid touch:grid-cols-[auto_minmax(0,1fr)_auto_auto] touch:items-center touch:gap-x-2 touch:gap-y-1.5 touch:px-3 touch:py-2"
    >
      <td className={`${cell} touch:order-1 touch:col-span-2 touch:min-w-0`}>
        <div className="flex min-w-0 items-center gap-2">
          <Avatar {...user} />
          <span className="truncate">{user.name}</span>
        </div>
      </td>
      <td className={`${cell} touch:order-4`}>
        <div className="flex flex-col items-end gap-0.5 touch:flex-row touch:items-center touch:gap-2">
          {readOnly ? (
            <>
              {/* A null days is "the default" of the calendar when it was stored; today's calendar may not match, so no number. */}
              <span data-testid={ids.capacityDays} className="tabular-nums">
                {isDefault ? 'default' : row.days}
              </span>
            </>
          ) : (
            <>
              <input
                data-testid={ids.capacityDays}
                className="field w-16 text-right tabular-nums"
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
              {isDefault ? (
                <span data-testid={ids.capacityDaysDefault} className={small}>
                  default
                </span>
              ) : (
                <button
                  type="button"
                  data-testid={ids.capacityDaysReset}
                  className="rounded-control text-[length:var(--text-xs)] text-signal underline touch:min-h-[var(--touch-target)] touch:px-1"
                  title={`Back to the default of ${shape.defaultDays} days`}
                  aria-label={`Reset days for ${user.name} to the default`}
                  onClick={() => onChange({ ...row, days: '' })}
                >
                  reset
                </button>
              )}
            </>
          )}
        </div>
      </td>
      <td className={`${cell} text-right tabular-nums touch:order-2`}>
        <span data-testid={ids.capacityPoints}>{points ?? '—'}</span>
        <span className="ml-1 text-ink-muted">pts</span>
      </td>
      <td className={`${cell} touch:order-5 touch:col-span-3 touch:min-w-0`}>
        {readOnly ? (
          <span data-testid={ids.capacityNote} className="text-ink-muted">
            {row.note}
          </span>
        ) : (
          <input
            data-testid={ids.capacityNote}
            className="field w-full"
            placeholder="Optional, e.g. Off Thu–Fri"
            aria-label={`Note for ${user.name}`}
            value={row.note}
            onChange={(event) => onChange({ ...row, note: event.target.value })}
          />
        )}
      </td>
      <td className={`${cell} text-center touch:order-3`}>
        {readOnly ? null : (
          <button
            type="button"
            data-testid={ids.capacityRemove}
            className="inline-flex size-7 touch:size-[var(--touch-target)] items-center justify-center rounded-control text-ink-muted hover:bg-signal-soft hover:text-ink"
            title={`Remove ${user.name} from this sprint's capacity`}
            aria-label={`Remove ${user.name}`}
            onClick={onRemove}
          >
            ×
          </button>
        )}
      </td>
    </tr>
  );
}
