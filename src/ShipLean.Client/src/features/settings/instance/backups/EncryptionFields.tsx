import { useId } from 'react';
import { backupsTestIds } from '../../../backups/testIds';
import { FieldError } from '../integrations/FieldError';
import type { BackupDraft } from './backupForm';
import type { BackupSettings } from './backupsApi';

export interface EncryptionFieldsProps {
  settings: BackupSettings;
  draft: BackupDraft;
  onChange: (patch: Partial<BackupDraft>) => void;
  /** The server's message for `passphrase`. */
  error: string | undefined;
  /** The page's own message when the two fields differ. */
  mismatch: string | undefined;
}

const input =
  'h-7 w-full max-w-md rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink';

/** The passphrase every backup file is encrypted with, typed twice. Untouched means the saved one stays. */
export function EncryptionFields({
  settings,
  draft,
  onChange,
  error,
  mismatch,
}: EncryptionFieldsProps) {
  const id = useId();
  const saved = settings.passphraseGeneration > 0;
  return (
    <div className="grid gap-2 text-[length:var(--text-sm)] font-medium text-ink">
      {saved ? (
        <div className="grid gap-1 font-normal">
          <span data-testid={backupsTestIds.backupsPassphraseInfo} className="text-ink">
            {`Passphrase #${settings.passphraseGeneration}, set ${settings.passphraseSetLabel ?? ''}`}
          </span>
          <span id={`${id}-note`} className="text-ink-muted">
            Typing a new one below starts the next generation: attachments are copied again, and
            backups already in the destination keep the old passphrase.
          </span>
        </div>
      ) : null}
      <label htmlFor={`${id}-passphrase`}>{saved ? 'New passphrase' : 'Passphrase'}</label>
      <input
        id={`${id}-passphrase`}
        data-testid={backupsTestIds.backupsPassphrase}
        type="password"
        className={input}
        value={draft.passphrase}
        autoComplete="new-password"
        spellCheck={false}
        aria-describedby={
          [...(saved ? [`${id}-note`] : []), ...(error === undefined ? [] : [`${id}-error`])].join(
            ' ',
          ) || undefined
        }
        onChange={(e) => onChange({ passphrase: e.target.value })}
      />
      <label htmlFor={`${id}-confirm`}>Type it again</label>
      <input
        id={`${id}-confirm`}
        data-testid={backupsTestIds.backupsPassphraseConfirm}
        type="password"
        className={input}
        value={draft.passphraseConfirm}
        autoComplete="new-password"
        spellCheck={false}
        aria-describedby={mismatch === undefined ? undefined : `${id}-mismatch`}
        onChange={(e) => onChange({ passphraseConfirm: e.target.value })}
      />
      <span className="font-normal text-caution">
        Without this passphrase these backups can&apos;t be restored. Keep it somewhere other than
        this server.
      </span>
      <span id={`${id}-mismatch`}>
        <FieldError message={mismatch} testId={backupsTestIds.backupsFieldError} />
      </span>
      <span id={`${id}-error`}>
        <FieldError message={error} testId={backupsTestIds.backupsFieldError} />
      </span>
    </div>
  );
}
