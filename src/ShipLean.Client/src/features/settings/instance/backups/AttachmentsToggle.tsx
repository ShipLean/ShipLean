import { backupsTestIds as ids } from '../../../backups/testIds';
import type { AttachmentStore } from './backupsApi';

export interface AttachmentsToggleProps {
  store: AttachmentStore;
  checked: boolean;
  onChange: (includeAttachments: boolean) => void;
}

function noteOf(store: AttachmentStore): string {
  if (store.inDatabaseBackup) {
    return 'Attachments are stored in the database that is backed up, so every database backup has them.';
  }
  if (store.kind === 's3') {
    return 'Your attachments are already in S3; turn this on only if you want a second copy.';
  }
  return "Attachments are on this server's local disk. Each one is copied once per passphrase generation, so later runs copy only new ones.";
}

/** Whether each run also copies the attachments. Off and locked when they are already inside the database backup. */
export function AttachmentsToggle({ store, checked, onChange }: AttachmentsToggleProps) {
  const inDatabase = store.inDatabaseBackup;
  return (
    <div className="grid gap-1 text-[length:var(--text-sm)] text-ink">
      <label className="flex items-center gap-2">
        <input
          type="checkbox"
          data-testid={ids.backupsAttachmentsToggle}
          checked={inDatabase || checked}
          disabled={inDatabase}
          onChange={(e) => onChange(e.target.checked)}
        />
        {inDatabase ? 'Already inside the database backup' : 'Also back up attachments'}
      </label>
      <span data-testid={ids.backupsAttachmentsNote} className="text-ink-muted">
        {noteOf(store)}
      </span>
    </div>
  );
}
