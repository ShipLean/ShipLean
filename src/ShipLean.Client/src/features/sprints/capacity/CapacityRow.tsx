import type { UserRef } from '../../../api/refs';
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

export const cell = 'px-3 py-1.5 align-middle';
const small = 'text-[length:var(--text-xs)] text-ink-muted';

/** One member: who, their days (the default until typed over), the capacity those days give and a note. */
export function CapacityRow({ line, shape, readOnly, onChange, onRemove }: CapacityRowProps) {
  const { user, row, points } = line;
  const isDefault = row.days === '';
  const shownDays = isDefault ? `${shape.defaultDays}` : row.days;
  return (
    <tr data-testid={ids.capacityRow} className="border-t border-edge">
      <td className={cell}>
        <div className="flex min-w-0 items-center gap-2">
          <Avatar {...user} />
          <span className="truncate">{user.name}</span>
        </div>
      </td>
      <td className={cell}>
        <div className="flex flex-col items-end gap-0.5">
          {readOnly ? (
            <>
              <span data-testid={ids.capacityDays} className="tabular-nums">
                {shownDays}
              </span>
              {isDefault ? <span className={small}>default</span> : null}
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
                value={shownDays}
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
                  className="rounded-control text-[length:var(--text-xs)] text-signal underline"
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
      <td className={`${cell} text-right tabular-nums`}>
        <span data-testid={ids.capacityPoints}>{points ?? '—'}</span>
        <span className="ml-1 text-ink-muted">pts</span>
      </td>
      <td className={cell}>
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
      <td className={`${cell} text-center`}>
        {readOnly ? null : (
          <button
            type="button"
            data-testid={ids.capacityRemove}
            className="inline-flex size-7 items-center justify-center rounded-control text-ink-muted hover:bg-signal-soft hover:text-ink"
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
