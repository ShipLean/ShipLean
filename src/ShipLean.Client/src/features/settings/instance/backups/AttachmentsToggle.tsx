import { backupsTestIds } from '../../../backups/testIds';
import type { AttachmentStore } from './backupsApi';

export interface AttachmentsToggleProps {
  store: AttachmentStore;
  checked: boolean;
  onChange: (checked: boolean) => void;
}

/** Whether attachments are copied to the destination too. Already covered when they live in the backed-up database. */
export function AttachmentsToggle({ store, checked, onChange }: AttachmentsToggleProps) {
  const inDatabase = store.inDatabaseBackup;
  return (
    <div className="grid gap-1 text-[length:var(--text-sm)]">
      <label className="flex items-center gap-2 font-medium text-ink">
        <input
          type="checkbox"
          data-testid={backupsTestIds.backupsAttachmentsToggle}
          checked={inDatabase || checked}
          disabled={inDatabase}
          onChange={(e) => onChange(e.target.checked)}
        />
        {inDatabase ? 'Already inside the database backup' : 'Also back up attachments'}
      </label>
      {inDatabase ? (
        <span data-testid={backupsTestIds.backupsAttachmentsNote} className="text-ink-muted">
          Attachments are stored in Postgres, in the same database, so every database backup has
          them.
        </span>
      ) : null}
      {store.kind === 's3' ? (
        <span data-testid={backupsTestIds.backupsAttachmentsNote} className="text-ink-muted">
          Your attachments are already in S3; turn this on only if you want a second copy.
        </span>
      ) : null}
    </div>
  );
}
