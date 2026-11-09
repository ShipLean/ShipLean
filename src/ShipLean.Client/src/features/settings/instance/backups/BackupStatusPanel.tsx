import { useEffect, useId } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ApiRequestError } from '../../../../api/client';
import { backupsTestIds } from '../../../backups/testIds';
import { Panel } from '../../draft/Panel';
import { errorText } from '../model';
import { backupsKey, useRunBackupNow, type BackupHealth, type BackupSettings } from './backupsApi';
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
  const next = settings.schedule.nextRuns[0]?.label;
  switch (settings.health) {
    case 'ok': {
      const took = newest?.durationLabel ? `, took ${newest.durationLabel}` : '';
      const last =
        settings.lastSuccessLabel === null
          ? ''
          : `Last backup ${settings.lastSuccessLabel}${took}.`;
      return [last, next ? `Next run ${next}.` : ''].filter(Boolean).join(' ');
    }
    case 'running': {
      const running = settings.runs.find((r) => r.state === 'running');
      if (running === undefined) return '';
      return running.startedBy
        ? `Started ${running.startedLabel} by ${running.startedBy.name}.`
        : `Started ${running.startedLabel}.`;
    }
    case 'failing': {
      const failure = newest?.error ? `${newest.startedLabel}: ${newest.error}` : '';
      return [failure, success].filter(Boolean).join(' ');
    }
    case 'overdue':
      return `A scheduled run did not happen. ${success}`.trim();
    case 'neverRun':
      return settings.enabled && next
        ? `First run ${next}, or back up now.`
        : 'Nothing has run yet. Press Back up now to start the first one.';
    case 'notConfigured':
      return 'Nothing is backed up yet. Add a destination and a passphrase, then save.';
  }
}

const whyBlocked = (health: BackupHealth): string | null =>
  health === 'notConfigured'
    ? 'Save the settings first'
    : health === 'running'
      ? 'One is already running'
      : null;

/** The health line, Back up now and the run history. Polling belongs to useBackupSettings. */
export function BackupStatusPanel({ settings }: BackupStatusPanelProps) {
  const runNow = useRunBackupNow();
  const queryClient = useQueryClient();
  const whyId = useId();
  const { reset } = runNow;
  // A stale run-now error goes when the health changes, except into running: a 409 is answered by exactly that
  // change, and the message stays until the run is over.
  useEffect(() => {
    if (settings.health !== 'running') reset();
  }, [settings.health, reset]);
  const view = HEALTH[settings.health];
  const why = whyBlocked(settings.health);
  const blocked = why !== null;
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
          <span role="status" className="flex flex-wrap items-center gap-2">
            <span className={`font-semibold ${view.tone}`}>{view.title}</span>
            <span className="text-ink-muted">{detailOf(settings)}</span>
          </span>
          <span className="flex-1" />
          {why !== null ? (
            <span id={whyId} className="text-[length:var(--text-xs)] text-ink-muted">
              {why}
            </span>
          ) : null}
          <button
            type="button"
            aria-describedby={why === null ? undefined : whyId}
            data-testid={backupsTestIds.backupsRunNow}
            className="btn"
            disabled={blocked || runNow.isPending}
            onClick={() =>
              runNow.mutate(undefined, {
                // Another run is going: re-read so health, the button and the history catch up.
                onError: (e) => {
                  if (e instanceof ApiRequestError && e.status === 409) {
                    void queryClient.invalidateQueries({ queryKey: backupsKey, exact: true });
                  }
                },
              })
            }
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
      <RunHistory runs={settings.runs} keepCount={settings.keepCount} />
    </>
  );
}
