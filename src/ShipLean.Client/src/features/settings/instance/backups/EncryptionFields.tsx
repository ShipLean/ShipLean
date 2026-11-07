import { useId } from 'react';
import { backupsTestIds as ids } from '../../../backups/testIds';
import { FieldError } from '../integrations/FieldError';
import type { BackupSettings } from './backupsApi';

const field = 'grid gap-1 text-[length:var(--text-sm)] font-medium text-ink';
const hint = 'font-normal text-ink-muted';
const input =
  'h-7 w-full max-w-md rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink disabled:opacity-60';

export interface EncryptionFieldsProps {
  settings: BackupSettings;
  passphrase: string;
  passphraseConfirm: string;
  passphraseError: string | undefined;
  confirmError: string | undefined;
  onPassphraseChange: (passphrase: string) => void;
  onConfirmChange: (passphraseConfirm: string) => void;
}

/**
 * The passphrase every uploaded file is encrypted with, typed twice. Once one is saved, its generation and when it was
 * set show above the fields, and leaving them blank keeps it.
 */
export function EncryptionFields({
  settings,
  passphrase,
  passphraseConfirm,
  passphraseError,
  confirmError,
  onPassphraseChange,
  onConfirmChange,
}: EncryptionFieldsProps) {
  const base = useId();
  const generation = settings.passphraseGeneration;
  const saved = generation > 0;
  const setLabel = settings.passphraseSetLabel;
  const passphraseId = `${base}-passphrase`;
  const confirmId = `${base}-confirm`;

  return (
    <div className="grid gap-3">
      {saved ? (
        <div className={field}>
          <span data-testid={ids.backupsPassphraseInfo}>
            {setLabel === null
              ? `Passphrase #${generation}`
              : `Passphrase #${generation}, set ${setLabel}`}
          </span>
          <span className={hint}>
            {`Changing it starts passphrase #${generation + 1}: attachments are copied again on the next run, and backups already made keep needing passphrase #${generation}.`}
          </span>
        </div>
      ) : null}
      <div className={field}>
        <label htmlFor={passphraseId}>{saved ? 'New passphrase' : 'Passphrase'}</label>
        <input
          id={passphraseId}
          aria-describedby={`${passphraseId}-error`}
          aria-invalid={passphraseError !== undefined}
          data-testid={ids.backupsPassphrase}
          type="password"
          className={input}
          value={passphrase}
          placeholder={saved ? 'Leave blank to keep it' : 'A long phrase you will keep'}
          autoComplete="new-password"
          spellCheck={false}
          onChange={(e) => onPassphraseChange(e.target.value)}
        />
        <span id={`${passphraseId}-error`} className="contents">
          <FieldError message={passphraseError} testId={ids.backupsFieldError} />
        </span>
      </div>
      <div className={field}>
        <label htmlFor={confirmId}>Type it again</label>
        <input
          id={confirmId}
          aria-describedby={`${confirmId}-error`}
          aria-invalid={confirmError !== undefined}
          data-testid={ids.backupsPassphraseConfirm}
          type="password"
          className={input}
          value={passphraseConfirm}
          autoComplete="new-password"
          spellCheck={false}
          onChange={(e) => onConfirmChange(e.target.value)}
        />
        <span id={`${confirmId}-error`} className="contents">
          <FieldError message={confirmError} testId={ids.backupsFieldError} />
        </span>
      </div>
      {!saved || passphrase !== '' ? (
        <p className="m-0 rounded-panel border border-caution px-3 py-2 text-[length:var(--text-sm)] text-ink">
          Without this passphrase these backups can&apos;t be restored. Keep it somewhere other than
          this server.
        </p>
      ) : null}
    </div>
  );
}
