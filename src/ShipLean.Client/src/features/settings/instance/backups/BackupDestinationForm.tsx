import { useEffect } from 'react';
import { backupsTestIds } from '../../../backups/testIds';
import { destinationValid, type BackupDraft, type BackupErrors } from './backupForm';
import type { BackupSettings } from './backupsApi';

export interface BackupDestinationFormProps {
  settings: BackupSettings;
  draft: BackupDraft;
  /** Merge a change into the draft. */
  onChange: (patch: Partial<BackupDraft>) => void;
  /** Server errors by DTO path. */
  errors: BackupErrors;
  /** Report this section's own validity on mount and whenever it changes. */
  onValidityChange: (valid: boolean) => void;
}

// TODO: replace with the real form: service, region or endpoint, bucket, prefix, keys, Test connection (it calls
// useTestBackupDestination itself), retention, passphrase with "Type it again", attachments. The section reports
// `destinationValid(draft, settings)` and shows `errors` next to its fields.
export function BackupDestinationForm({
  settings,
  draft,
  onChange,
  onValidityChange,
}: BackupDestinationFormProps) {
  const valid = destinationValid(draft, settings);
  useEffect(() => onValidityChange(valid), [valid, onValidityChange]);
  return (
    <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
      Bucket
      <input
        data-testid={backupsTestIds.backupsBucket}
        value={draft.bucket}
        onChange={(e) => onChange({ bucket: e.target.value })}
      />
    </label>
  );
}
