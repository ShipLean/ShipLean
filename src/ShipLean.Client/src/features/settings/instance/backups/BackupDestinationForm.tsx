import { useEffect, useId, useState } from 'react';
import { backupsTestIds } from '../../../backups/testIds';
import { FieldError } from '../integrations/FieldError';
import { TokenField } from '../integrations/TokenField';
import type { TokenEdit } from '../integrations/model';
import { AttachmentsToggle } from './AttachmentsToggle';
import { EncryptionFields } from './EncryptionFields';
import { RetentionFields } from './RetentionFields';
import {
  destinationProblems,
  toDestinationSaveDto,
  withEndpointMode,
  type BackupDraft,
  type BackupErrors,
  type EndpointMode,
} from './backupForm';
import { useTestBackupDestination, type BackupSettings, type BackupTestStep } from './backupsApi';
import { errorText } from '../model';

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

const input =
  'h-7 w-full max-w-md rounded-control border border-edge bg-panel px-2 font-mono text-[length:var(--text-sm)] text-ink disabled:opacity-60';
const field = 'grid gap-1 text-[length:var(--text-sm)] font-medium text-ink';
const hint = 'font-normal text-ink-muted';

const stepName: Record<BackupTestStep, string> = {
  write: 'Write',
  read: 'Read',
  delete: 'Delete',
};

const isHttp = (url: string) => url.trim().toLowerCase().startsWith('http:');

/** The destination (S3 or a compatible store), keys, retention, passphrase and attachments, with Test connection. */
export function BackupDestinationForm({
  settings,
  draft,
  onChange,
  errors,
  onValidityChange,
}: BackupDestinationFormProps) {
  const id = useId();
  const problems = destinationProblems(draft, settings);
  const valid = Object.keys(problems).length === 0;
  useEffect(() => onValidityChange(valid), [valid, onValidityChange]);

  const test = useTestBackupDestination();
  const change = (patch: Partial<BackupDraft>) => {
    test.reset();
    onChange(patch);
  };

  const lastFour = settings.destination?.s3.secretKeyLast4 ?? null;
  // Replace was pressed for this saved secret; a new saved secret (after Save) starts untouched again.
  const [replaced, setReplaced] = useState<string | null | undefined>(undefined);
  const replacing = replaced === lastFour;
  const secretEdit: TokenEdit = { replacing, clear: draft.removeKeys, value: draft.secretKey };

  const custom = draft.endpointMode === 'custom';
  const switchMode = (mode: EndpointMode) =>
    change({ ...withEndpointMode(draft, mode, settings), forcePathStyle: mode === 'custom' });
  const describedBy = (path: string, message: string | undefined) =>
    message === undefined ? undefined : `${id}-${path}`;
  const error = (path: string, message: string | undefined) => (
    <span id={`${id}-${path}`}>
      <FieldError message={message} testId={backupsTestIds.backupsFieldError} />
    </span>
  );

  return (
    <div className="grid gap-4">
      <div className="grid gap-3">
        <label className={field}>
          Service
          <select
            data-testid={backupsTestIds.backupsEndpointMode}
            className={`${input} font-sans`}
            value={draft.endpointMode}
            onChange={(e) => switchMode(e.target.value as EndpointMode)}
          >
            <option value="aws">AWS region</option>
            <option value="custom">Custom endpoint</option>
          </select>
        </label>
        {custom ? (
          <>
            <label className={field}>
              Endpoint URL
              <input
                data-testid={backupsTestIds.backupsEndpointUrl}
                className={input}
                value={draft.endpointUrl}
                spellCheck={false}
                autoComplete="off"
                aria-describedby={describedBy('endpoint', errors['destination.s3.endpointUrl'])}
                onChange={(e) => change({ endpointUrl: e.target.value })}
              />
              <span className={hint}>
                Private addresses are fine. No user name, query or fragment in the URL.
              </span>
              {isHttp(draft.endpointUrl) ? (
                <span
                  data-testid={backupsTestIds.backupsHttpWarning}
                  className="font-normal text-caution"
                >
                  Keys are sent unencrypted over http
                </span>
              ) : null}
            </label>
            {error('endpoint', errors['destination.s3.endpointUrl'])}
            <label className={field}>
              Signing region
              <input
                data-testid={backupsTestIds.backupsRegionSelect}
                className={input}
                value={draft.region}
                spellCheck={false}
                autoComplete="off"
                aria-describedby={describedBy('region', errors['destination.s3.region'])}
                onChange={(e) => change({ region: e.target.value })}
              />
              <span className={hint}>Whatever your store expects; most accept us-east-1.</span>
            </label>
            <label className="flex items-center gap-2 text-[length:var(--text-sm)] font-medium text-ink">
              <input
                type="checkbox"
                data-testid={backupsTestIds.backupsPathStyle}
                checked={draft.forcePathStyle}
                onChange={(e) => change({ forcePathStyle: e.target.checked })}
              />
              Path-style (bucket in the path, not the host name)
            </label>
          </>
        ) : (
          <label className={field}>
            Region
            <select
              data-testid={backupsTestIds.backupsRegionSelect}
              className={`${input} font-sans`}
              value={draft.region}
              aria-describedby={describedBy('region', errors['destination.s3.region'])}
              onChange={(e) => change({ region: e.target.value })}
            >
              {settings.regions.map((r) => (
                <option key={r.code} value={r.code}>
                  {`${r.name} · ${r.code}`}
                </option>
              ))}
            </select>
          </label>
        )}
        {error('region', errors['destination.s3.region'])}
        <label className={field}>
          Bucket
          <input
            data-testid={backupsTestIds.backupsBucket}
            className={input}
            value={draft.bucket}
            placeholder="shiplean-backups"
            spellCheck={false}
            autoComplete="off"
            aria-describedby={describedBy('bucket', errors['destination.s3.bucket'])}
            onChange={(e) => change({ bucket: e.target.value })}
          />
        </label>
        {error('bucket', errors['destination.s3.bucket'])}
        <label className={field}>
          Prefix
          <input
            data-testid={backupsTestIds.backupsPrefix}
            className={input}
            value={draft.prefix}
            spellCheck={false}
            autoComplete="off"
            onChange={(e) => change({ prefix: e.target.value })}
          />
          <span className={hint}>
            A folder inside the bucket. Database backups go in {draft.prefix}database/, attachments
            in {draft.prefix}attachments/.
          </span>
        </label>
        <label className={field}>
          Access key ID
          <input
            data-testid={backupsTestIds.backupsAccessKeyId}
            className={input}
            value={draft.removeKeys ? '' : draft.accessKeyId}
            disabled={draft.removeKeys}
            placeholder="Optional"
            spellCheck={false}
            autoComplete="off"
            aria-describedby={describedBy('access-key', errors['destination.s3.accessKeyId'])}
            onChange={(e) => change({ accessKeyId: e.target.value })}
          />
        </label>
        {error('access-key', errors['destination.s3.accessKeyId'] ?? problems.accessKeyId)}
        <TokenField
          label="Secret key"
          hint="Stored encrypted and never shown again. Leave it blank to keep the saved one."
          lastFour={lastFour}
          fromEnv={false}
          edit={secretEdit}
          ids={{
            input: backupsTestIds.backupsSecretKey,
            lastFour: `${backupsTestIds.backupsSecretKey}-last-four`,
            replace: `${backupsTestIds.backupsSecretKey}-replace`,
            clear: `${backupsTestIds.backupsSecretKey}-clear`,
            env: `${backupsTestIds.backupsSecretKey}-env`,
          }}
          disabled={false}
          error={problems.secretKey}
          onChange={(edit) => {
            setReplaced(edit.replacing ? lastFour : undefined);
            change({ removeKeys: edit.clear, secretKey: edit.value });
          }}
        />
        <div className="flex flex-wrap items-center gap-2 text-[length:var(--text-sm)]">
          <button
            type="button"
            data-testid={backupsTestIds.backupsTestButton}
            className="btn"
            disabled={problems.bucket !== undefined || test.isPending}
            onClick={() => test.mutate(toDestinationSaveDto(draft))}
          >
            Test connection
          </button>
          {test.data ? (
            <span
              role={test.data.ok ? 'status' : 'alert'}
              data-testid={backupsTestIds.backupsTestResult}
              className={test.data.ok ? 'text-good' : 'text-alert'}
            >
              {test.data.ok
                ? test.data.message
                : `${test.data.step === null ? 'Failed' : `${stepName[test.data.step]} failed`}: ${test.data.message}`}
            </span>
          ) : null}
          {test.isError ? (
            <span
              role="alert"
              data-testid={backupsTestIds.backupsTestResult}
              className="text-alert"
            >
              {errorText(test.error)}
            </span>
          ) : null}
        </div>
      </div>
      <RetentionFields
        draft={draft}
        onChange={change}
        error={errors.keepCount ?? problems.keepCount}
      />
      <EncryptionFields
        settings={settings}
        draft={draft}
        onChange={change}
        error={errors.passphrase}
        mismatch={problems.passphraseConfirm}
      />
      <AttachmentsToggle
        store={settings.attachmentStore}
        checked={draft.includeAttachments}
        onChange={(includeAttachments) => change({ includeAttachments })}
      />
    </div>
  );
}
