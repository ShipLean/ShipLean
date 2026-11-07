import { useEffect, useId, useState, type ReactNode } from 'react';
import { backupsTestIds as ids } from '../../../backups/testIds';
import { FieldError } from '../integrations/FieldError';
import { AttachmentsToggle } from './AttachmentsToggle';
import { EncryptionFields } from './EncryptionFields';
import { RetentionFields } from './RetentionFields';
import {
  destinationProblems,
  mapSaveError,
  toDestinationSaveDto,
  withEndpointMode,
  type BackupDraft,
  type BackupErrors,
  type DraftField,
  type EndpointMode,
  type ErrorPath,
} from './backupForm';
import {
  useTestBackupDestination,
  type BackupSettings,
  type BackupTestResult,
  type BackupTestStep,
} from './backupsApi';

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

const field = 'grid gap-1 text-[length:var(--text-sm)] font-medium text-ink';
const hint = 'font-normal text-ink-muted';
const input =
  'h-7 w-full max-w-md rounded-control border border-edge bg-panel px-2 font-mono text-[length:var(--text-sm)] text-ink disabled:opacity-60';
const subhead = 'm-0 text-[length:var(--text-sm)] font-semibold text-ink';

/** The fields Test connection sends; a problem in any of them leaves the button off. */
const connectionFields: readonly DraftField[] = [
  'bucket',
  'region',
  'endpointUrl',
  'accessKeyId',
  'secretKey',
];

const stepNames: Record<BackupTestStep, string> = {
  write: 'Write',
  read: 'Read',
  delete: 'Delete',
};

const isHttp = (url: string) => url.trim().toLowerCase().startsWith('http://');

function testOutcome(result: BackupTestResult): string {
  if (result.ok) return `✓ ${result.message}`;
  return result.step === null
    ? `✕ ${result.message}`
    : `✕ ${stepNames[result.step]} failed: ${result.message}`;
}

/** A label, its control, a hint and the error the control is described by. */
function Field({
  label,
  id,
  hintText,
  error,
  children,
}: {
  label: string;
  id: string;
  hintText?: ReactNode;
  error: string | undefined;
  children: ReactNode;
}) {
  return (
    <div className={field}>
      <label htmlFor={id}>{label}</label>
      {children}
      {hintText === undefined ? null : <span className={hint}>{hintText}</span>}
      <span id={`${id}-error`} className="contents">
        <FieldError message={error} testId={ids.backupsFieldError} />
      </span>
    </div>
  );
}

/**
 * Where backups go and how they are kept: the S3 destination with Test connection, then retention, the passphrase and
 * attachments. All of it is one section of the tab's draft; the shell saves it.
 */
export function BackupDestinationForm({
  settings,
  draft,
  onChange,
  errors,
  onValidityChange,
}: BackupDestinationFormProps) {
  const base = useId();
  const test = useTestBackupDestination();
  // A field's own problem shows once it has been edited; the server's errors show at once.
  const [touched, setTouched] = useState<ReadonlySet<DraftField>>(() => new Set());

  const problems = destinationProblems(draft, settings);
  const valid = Object.keys(problems).length === 0;
  useEffect(() => onValidityChange(valid), [valid, onValidityChange]);

  const tested = mapSaveError(test.error);
  const shown = (path: ErrorPath | null, key: DraftField): string | undefined => {
    const server = path === null ? undefined : (errors[path] ?? tested.errors[path]);
    return server ?? (touched.has(key) ? problems[key] : undefined);
  };
  const edit = (patch: Partial<BackupDraft>, key?: DraftField) => {
    if (key !== undefined && !touched.has(key)) setTouched(new Set(touched).add(key));
    onChange(patch);
  };
  // Anything Test connection sends: a result about the old values no longer applies.
  const editConnection = (patch: Partial<BackupDraft>, key?: DraftField) => {
    test.reset();
    edit(patch, key);
  };
  const switchMode = (mode: EndpointMode) => {
    const next = withEndpointMode(draft, mode, settings);
    // Most S3-compatible stores (Ceph, MinIO) need path-style addressing.
    editConnection(mode === 'custom' && next !== draft ? { ...next, forcePathStyle: true } : next);
  };

  const custom = draft.endpointMode === 'custom';
  const saved = settings.destination?.s3;
  const savedLast4 = draft.removeKeys ? null : (saved?.secretKeyLast4 ?? null);
  const hasSavedKeys = saved?.accessKeyId != null || saved?.secretKeyLast4 != null;
  const canTest = !test.isPending && connectionFields.every((key) => problems[key] === undefined);
  const idOf = (name: string) => `${base}-${name}`;
  const describe = (name: string, error: string | undefined) => ({
    id: idOf(name),
    'aria-describedby': `${idOf(name)}-error`,
    'aria-invalid': error !== undefined,
  });

  const regionError = shown('destination.s3.region', 'region');
  const endpointError = shown('destination.s3.endpointUrl', 'endpointUrl');
  const bucketError = shown('destination.s3.bucket', 'bucket');
  const accessKeyError = shown('destination.s3.accessKeyId', 'accessKeyId');
  const secretError = shown(null, 'secretKey');
  const prefix = draft.prefix.trim();
  let secretHint: string | undefined;
  if (savedLast4 !== null) {
    secretHint = 'Stored encrypted and never shown again. Leave it blank to keep the saved one.';
  } else if (!custom && !draft.removeKeys && draft.accessKeyId.trim() === '') {
    secretHint =
      'Both blank: ShipLean uses the AWS default credential chain (instance role, environment, or shared config on the server).';
  }

  return (
    <div className="grid gap-4">
      <div className="grid gap-3">
        <div className={field}>
          <label htmlFor={idOf('mode')}>Service</label>
          <select
            id={idOf('mode')}
            data-testid={ids.backupsEndpointMode}
            className={input}
            value={draft.endpointMode}
            onChange={(e) => switchMode(e.target.value as EndpointMode)}
          >
            <option value="aws">Amazon S3</option>
            <option value="custom">S3-compatible endpoint</option>
          </select>
          <span className={hint}>S3, or any S3-compatible store such as Ceph or MinIO.</span>
        </div>
        {custom ? (
          <>
            <Field
              label="Endpoint URL"
              id={idOf('endpoint')}
              error={endpointError}
              hintText="Private addresses are fine (an on-premises Ceph, for example). No user name, query or fragment in the URL."
            >
              <input
                {...describe('endpoint', endpointError)}
                data-testid={ids.backupsEndpointUrl}
                className={input}
                value={draft.endpointUrl}
                placeholder="https://s3.example.com"
                spellCheck={false}
                autoComplete="off"
                onChange={(e) => editConnection({ endpointUrl: e.target.value }, 'endpointUrl')}
              />
              {isHttp(draft.endpointUrl) ? (
                <span
                  role="status"
                  data-testid={ids.backupsHttpWarning}
                  className="font-normal text-caution"
                >
                  ⚠ Keys are sent unencrypted over http. The backups themselves are encrypted with
                  your passphrase either way.
                </span>
              ) : null}
            </Field>
            <Field
              label="Signing region"
              id={idOf('region')}
              error={regionError}
              hintText="Whatever your store expects; most accept us-east-1."
            >
              <input
                {...describe('region', regionError)}
                data-testid={ids.backupsRegionSelect}
                className={`${input} max-w-40`}
                value={draft.region}
                spellCheck={false}
                autoComplete="off"
                onChange={(e) => editConnection({ region: e.target.value }, 'region')}
              />
            </Field>
            <div className={field}>
              <label className="flex items-center gap-2 font-normal">
                <input
                  type="checkbox"
                  data-testid={ids.backupsPathStyle}
                  checked={draft.forcePathStyle}
                  onChange={(e) => editConnection({ forcePathStyle: e.target.checked })}
                />
                Path-style (bucket in the path, not the host name)
              </label>
              <span className={hint}>Most Ceph and MinIO setups need this.</span>
            </div>
          </>
        ) : (
          <Field label="Region" id={idOf('region')} error={regionError}>
            <select
              {...describe('region', regionError)}
              data-testid={ids.backupsRegionSelect}
              className={input}
              value={draft.region}
              onChange={(e) => editConnection({ region: e.target.value }, 'region')}
            >
              {settings.regions.map((r) => (
                <option key={r.code} value={r.code}>
                  {`${r.name} · ${r.code}`}
                </option>
              ))}
            </select>
          </Field>
        )}
        <Field label="Bucket" id={idOf('bucket')} error={bucketError}>
          <input
            {...describe('bucket', bucketError)}
            data-testid={ids.backupsBucket}
            className={input}
            value={draft.bucket}
            placeholder="shiplean-backups"
            spellCheck={false}
            autoComplete="off"
            onChange={(e) => editConnection({ bucket: e.target.value }, 'bucket')}
          />
        </Field>
        <Field
          label="Prefix"
          id={idOf('prefix')}
          error={undefined}
          hintText={
            <>
              A folder inside the bucket. Database backups go in <code>{`${prefix}database/`}</code>
              , attachments in <code>{`${prefix}attachments/`}</code>.
            </>
          }
        >
          <input
            {...describe('prefix', undefined)}
            data-testid={ids.backupsPrefix}
            className={input}
            value={draft.prefix}
            spellCheck={false}
            autoComplete="off"
            onChange={(e) => editConnection({ prefix: e.target.value })}
          />
        </Field>
        <Field label="Access key ID" id={idOf('access-key')} error={accessKeyError}>
          <input
            {...describe('access-key', accessKeyError)}
            data-testid={ids.backupsAccessKeyId}
            className={input}
            value={draft.accessKeyId}
            placeholder={custom ? undefined : 'Optional'}
            spellCheck={false}
            autoComplete="off"
            disabled={draft.removeKeys}
            onChange={(e) => editConnection({ accessKeyId: e.target.value }, 'accessKeyId')}
          />
        </Field>
        <Field label="Secret key" id={idOf('secret')} error={secretError} hintText={secretHint}>
          <div className="flex items-center gap-2">
            <input
              {...describe('secret', secretError)}
              data-testid={ids.backupsSecretKey}
              type="password"
              className={input}
              value={draft.secretKey}
              placeholder={savedLast4 !== null ? `Saved · ends ${savedLast4}` : 'Paste the secret'}
              autoComplete="off"
              spellCheck={false}
              disabled={draft.removeKeys}
              onChange={(e) => editConnection({ secretKey: e.target.value }, 'secretKey')}
            />
            {draft.removeKeys ? (
              <>
                <span className="font-normal text-ink-muted">Removed when you save</span>
                <button
                  type="button"
                  className="btn-ghost"
                  onClick={() =>
                    editConnection({ removeKeys: false, accessKeyId: saved?.accessKeyId ?? '' })
                  }
                >
                  Keep the keys
                </button>
              </>
            ) : null}
            {hasSavedKeys && !draft.removeKeys ? (
              <button
                type="button"
                className="btn-ghost"
                onClick={() => editConnection({ removeKeys: true, accessKeyId: '', secretKey: '' })}
              >
                Remove keys
              </button>
            ) : null}
          </div>
        </Field>
        <div className="flex flex-wrap items-center gap-2 text-[length:var(--text-sm)]">
          <button
            type="button"
            data-testid={ids.backupsTestButton}
            className="btn-ghost"
            disabled={!canTest}
            onClick={() => test.mutate(toDestinationSaveDto(draft))}
          >
            Test connection
          </button>
          {test.data ? (
            <span
              role={test.data.ok ? 'status' : 'alert'}
              data-testid={ids.backupsTestResult}
              className={test.data.ok ? 'text-good' : 'text-alert'}
            >
              {testOutcome(test.data)}
            </span>
          ) : null}
          {tested.form !== undefined ? (
            <span role="alert" data-testid={ids.backupsTestResult} className="text-alert">
              {`✕ ${tested.form}`}
            </span>
          ) : null}
          {test.data === undefined && tested.form === undefined ? (
            <span className="text-ink-muted">Writes, reads and deletes one small test object.</span>
          ) : null}
        </div>
      </div>
      <h3 className={subhead}>Retention</h3>
      <RetentionFields
        value={draft.keepCount}
        error={shown('keepCount', 'keepCount')}
        onChange={(keepCount) => edit({ keepCount }, 'keepCount')}
      />
      <h3 className={subhead}>Encryption</h3>
      <EncryptionFields
        settings={settings}
        passphrase={draft.passphrase}
        passphraseConfirm={draft.passphraseConfirm}
        passphraseError={shown('passphrase', 'passphrase')}
        confirmError={shown(null, 'passphraseConfirm')}
        onPassphraseChange={(passphrase) => edit({ passphrase }, 'passphrase')}
        onConfirmChange={(passphraseConfirm) => edit({ passphraseConfirm }, 'passphraseConfirm')}
      />
      <h3 className={subhead}>Attachments</h3>
      <AttachmentsToggle
        store={settings.attachmentStore}
        checked={draft.includeAttachments}
        onChange={(includeAttachments) => edit({ includeAttachments })}
      />
    </div>
  );
}
