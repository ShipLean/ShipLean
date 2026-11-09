import { useId } from 'react';
import { backupsTestIds } from '../../../backups/testIds';
import { FieldError } from '../integrations/FieldError';
import { KEEP_COUNT_MAX, KEEP_COUNT_MIN, type BackupDraft } from './backupForm';

export interface RetentionFieldsProps {
  draft: BackupDraft;
  onChange: (patch: Partial<BackupDraft>) => void;
  /** The server's message for `keepCount`, or the page's own reading of the number. */
  error: string | undefined;
}

/** How many database backups ShipLean keeps in the destination. */
export function RetentionFields({ draft, onChange, error }: RetentionFieldsProps) {
  const errorId = useId();
  return (
    <div className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
      <label htmlFor={`${errorId}-input`}>Keep the last</label>
      <div className="flex items-center gap-2 font-normal">
        <input
          id={`${errorId}-input`}
          data-testid={backupsTestIds.backupsKeepCount}
          type="number"
          inputMode="numeric"
          min={KEEP_COUNT_MIN}
          max={KEEP_COUNT_MAX}
          className="h-7 w-20 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink"
          value={draft.keepCount}
          aria-describedby={error === undefined ? `${errorId}-hint` : `${errorId}-hint ${errorId}`}
          onChange={(e) => onChange({ keepCount: e.target.value })}
        />
        <span>database backups</span>
      </div>
      <span id={`${errorId}-hint`} className="font-normal text-ink-muted">
        {`${KEEP_COUNT_MIN} to ${KEEP_COUNT_MAX}. After each successful backup, older ones ShipLean wrote to this destination are deleted. Attachment copies are never deleted, and files ShipLean did not write are never touched.`}
      </span>
      <span id={errorId}>
        <FieldError message={error} testId={backupsTestIds.backupsFieldError} />
      </span>
    </div>
  );
}
