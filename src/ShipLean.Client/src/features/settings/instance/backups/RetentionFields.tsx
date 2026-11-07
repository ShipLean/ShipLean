import { useId } from 'react';
import { backupsTestIds as ids } from '../../../backups/testIds';
import { FieldError } from '../integrations/FieldError';
import { KEEP_COUNT_MAX, KEEP_COUNT_MIN } from './backupForm';

export interface RetentionFieldsProps {
  /** The keep count as typed. */
  value: string;
  error: string | undefined;
  onChange: (keepCount: string) => void;
}

/** How many database backups ShipLean keeps in the destination before it deletes the oldest. */
export function RetentionFields({ value, error, onChange }: RetentionFieldsProps) {
  const id = useId();
  return (
    <div className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
      <div className="flex items-center gap-2 font-normal">
        <label htmlFor={id}>Keep the last</label>
        <input
          id={id}
          aria-describedby={`${id}-error`}
          aria-invalid={error !== undefined}
          data-testid={ids.backupsKeepCount}
          type="number"
          min={KEEP_COUNT_MIN}
          max={KEEP_COUNT_MAX}
          className="h-7 w-20 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink"
          value={value}
          onChange={(e) => onChange(e.target.value)}
        />
        <span>database backups</span>
      </div>
      <span className="font-normal text-ink-muted">
        {`${KEEP_COUNT_MIN} to ${KEEP_COUNT_MAX}. After each successful backup, older ones ShipLean wrote to this destination are deleted. Attachment copies are never deleted, and files ShipLean didn't write are never touched.`}
      </span>
      <span id={`${id}-error`} className="contents">
        <FieldError message={error} testId={ids.backupsFieldError} />
      </span>
    </div>
  );
}
