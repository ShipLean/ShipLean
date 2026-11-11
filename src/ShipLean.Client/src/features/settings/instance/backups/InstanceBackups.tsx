import { useCallback, useState } from 'react';
import { backupsTestIds } from '../../../backups/testIds';
import { Panel } from '../../draft/Panel';
import { AccessState } from '../AccessState';
import { BackupDestinationForm } from './BackupDestinationForm';
import { BackupStatusPanel } from './BackupStatusPanel';
import { CronField } from './CronField';
import {
  NOTHING_REPORTED,
  canSave,
  draftOf,
  isDirty,
  mapSaveError,
  toSaveDto,
  type BackupDraft,
  type SectionValidity,
} from './backupForm';
import { useBackupSettings, useSaveBackups, type BackupSettings } from './backupsApi';

/** The instance admin's Backups tab: status, destination, schedule, all under one Save. */
export function InstanceBackups() {
  const settings = useBackupSettings();
  // A failed re-read while the data is on screen (the 5-second poll) must not replace the form and lose the draft.
  return (
    <AccessState pending={settings.isPending} error={settings.data ? null : settings.error}>
      {settings.data ? <BackupsForm settings={settings.data} /> : null}
    </AccessState>
  );
}

function BackupsForm({ settings }: { settings: BackupSettings }) {
  const [draft, setDraft] = useState<BackupDraft>(() => draftOf(settings));
  const [validity, setValidity] = useState<SectionValidity>(NOTHING_REPORTED);
  const save = useSaveBackups();

  // Stable, so a section's "report on change" effect runs when its validity changes and not on every render.
  const reportDestination = useCallback(
    (valid: boolean) =>
      setValidity((v) => (v.destination === valid ? v : { ...v, destination: valid })),
    [],
  );
  const reportCron = useCallback(
    (valid: boolean) => setValidity((v) => (v.cron === valid ? v : { ...v, cron: valid })),
    [],
  );

  const change = (patch: Partial<BackupDraft>) => {
    save.reset();
    setDraft((d) => ({ ...d, ...patch }));
  };
  const dirty = isDirty(settings, draft);
  const saveable = canSave({ dirty, validity, saving: save.isPending });
  const { errors, form } = mapSaveError(save.error);

  return (
    <div data-testid={backupsTestIds.backupsPage} className="p-4">
      <BackupStatusPanel settings={settings} />
      <Panel title="Destination">
        <BackupDestinationForm
          settings={settings}
          draft={draft}
          onChange={change}
          errors={errors}
          onValidityChange={reportDestination}
        />
      </Panel>
      <Panel title="Schedule">
        <label className="flex items-center gap-2 text-[length:var(--text-sm)] font-medium text-ink">
          <input
            type="checkbox"
            data-testid={backupsTestIds.backupsEnabledToggle}
            checked={draft.enabled}
            disabled={save.isPending}
            onChange={(e) => change({ enabled: e.target.checked })}
          />
          Scheduled backups on
        </label>
        <CronField
          value={draft.cron}
          onChange={(cron) => change({ cron })}
          error={errors.cron}
          onValidityChange={reportCron}
        />
      </Panel>
      {form !== undefined ? (
        <p
          role="alert"
          data-testid={backupsTestIds.backupsFormError}
          className="m-0 mb-3 text-[length:var(--text-sm)] text-alert"
        >
          {form}
        </p>
      ) : null}
      <div className="flex items-center gap-2 text-[length:var(--text-sm)]">
        <span className="text-ink-muted">
          {dirty
            ? 'Unsaved changes'
            : settings.updatedLabel !== null
              ? `Saved ${settings.updatedLabel}`
              : ''}
        </span>
        <span className="flex-1" />
        <button
          type="button"
          data-testid={backupsTestIds.backupsSaveButton}
          className="btn-signal"
          disabled={!saveable}
          onClick={() => save.mutate(toSaveDto(draft), { onSuccess: (s) => setDraft(draftOf(s)) })}
        >
          Save
        </button>
      </div>
    </div>
  );
}
