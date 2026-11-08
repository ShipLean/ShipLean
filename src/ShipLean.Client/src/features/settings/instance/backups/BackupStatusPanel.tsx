import { ApiRequestError } from '../../../../api/client';
import { backupsTestIds } from '../../../backups/testIds';
import { Panel } from '../../draft/Panel';
import { errorText } from '../model';
import { useRunBackupNow, type BackupHealth, type BackupSettings } from './backupsApi';
import { RunHistory } from './RunHistory';

export const ALREADY_RUNNING = 'A backup is already running.';

export interface BackupStatusPanelProps {
  settings: BackupSettings;
}

interface HealthView {
  title: string;
  tone: string;
}

const HEALTH: Record<BackupHealth, HealthView> = {
  ok: { title: 'Healthy', tone: 'text-ok' },
  running: { title: 'Backing up now', tone: 'text-info' },
  failing: { title: 'Last backup failed', tone: 'text-danger' },
  overdue: { title: 'Overdue', tone: 'text-warn' },
  neverRun: { title: 'No backups yet', tone: 'text-ink-muted' },
  notConfigured: { title: 'Not set up', tone: 'text-ink-muted' },
};

// Only the server's own labels and messages are used; nothing here is built from a timestamp.
function detailOf(settings: BackupSettings): string {
  const success =
    settings.lastSuccessLabel === null ? '' : `Last success ${settings.lastSuccessLabel}.`;
  const newest = settings.runs[0];
  switch (settings.health) {
    case 'ok':
      return success;
    case 'running':
      return newest?.startedBy
        ? `Started ${newest.startedLabel} by ${newest.startedBy.name}.`
        : `Started ${newest?.startedLabel ?? 'just now'}.`;
    case 'failing':
      return [newest?.error, success].filter(Boolean).join(' ');
    case 'overdue':
      return `A scheduled run did not happen. ${success}`.trim();
    case 'neverRun':
      return 'Nothing has run yet. The first run is at the next scheduled time, or press Back up now.';
    case 'notConfigured':
      return 'Nothing is backed up yet. Add a destination and a passphrase, then save.';
  }
}

/** The health line, Back up now and the run history. Polling belongs to useBackupSettings. */
export function BackupStatusPanel({ settings }: BackupStatusPanelProps) {
  const runNow = useRunBackupNow();
  const view = HEALTH[settings.health];
  const blocked = settings.health === 'running' || settings.health === 'notConfigured';
  const error = runNow.error;
  const errorMessage =
    error === null
      ? null
      : error instanceof ApiRequestError && error.status === 409
        ? ALREADY_RUNNING
        : errorText(error);

  return (
    <>
      <Panel title="Status">
        <div
          data-testid={backupsTestIds.backupsHealth}
          data-health={settings.health}
          className="flex flex-wrap items-center gap-2 text-[length:var(--text-sm)]"
        >
          <span className={`font-semibold ${view.tone}`}>{view.title}</span>
          <span className="text-ink-muted">{detailOf(settings)}</span>
          <span className="flex-1" />
          <button
            type="button"
            data-testid={backupsTestIds.backupsRunNow}
            className="btn"
            disabled={blocked || runNow.isPending}
            onClick={() => runNow.mutate()}
          >
            Back up now
          </button>
        </div>
        {errorMessage !== null ? (
          <p
            role="alert"
            data-testid={backupsTestIds.backupsRunNowError}
            className="m-0 text-[length:var(--text-sm)] text-alert"
          >
            {errorMessage}
          </p>
        ) : null}
      </Panel>
      <RunHistory runs={settings.runs} />
    </>
  );
}
