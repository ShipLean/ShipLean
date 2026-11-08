import { useId, useState } from 'react';
import { backupsTestIds } from '../../../backups/testIds';
import { formatSize } from '../../../workItem/side/fileSize';
import { Panel } from '../../draft/Panel';
import type { BackupRun } from './backupsApi';

const RESULT: Record<BackupRun['state'], { text: string; tone: string }> = {
  running: { text: 'Running', tone: 'text-info' },
  succeeded: { text: 'Succeeded', tone: 'text-ok' },
  failed: { text: 'Failed', tone: 'text-danger' },
};

function RunRow({ run }: { run: BackupRun }) {
  const [open, setOpen] = useState(false);
  const errorId = useId();
  const result = RESULT[run.state];
  const trigger =
    run.trigger === 'manual'
      ? `Manual${run.startedBy ? `, ${run.startedBy.name}` : ''}`
      : 'Scheduled';
  return (
    <tr
      data-testid={backupsTestIds.backupsRunRow}
      data-state={run.state}
      data-pruned={run.pruned}
      className={`align-top ${run.pruned ? 'opacity-50' : ''}`}
    >
      <td className="px-3 py-2">{run.startedLabel}</td>
      <td className="px-3 py-2">{trigger}</td>
      <td className="px-3 py-2">
        <span className={`font-medium ${result.tone}`}>{result.text}</span>
        {run.pruned ? <span className="ml-2 text-ink-muted">Pruned</span> : null}
        {run.error !== null ? (
          <div>
            <button
              type="button"
              data-testid={backupsTestIds.backupsRunErrorToggle}
              className="p-0 text-[length:var(--text-xs)] text-ink-muted underline"
              aria-expanded={open}
              aria-controls={errorId}
              aria-label={`${open ? 'Hide' : 'Show'} error for the run started ${run.startedLabel}`}
              onClick={() => setOpen((v) => !v)}
            >
              {open ? 'Hide error' : 'Show error'}
            </button>
            {open ? (
              <p
                id={errorId}
                data-testid={backupsTestIds.backupsRunError}
                className="m-0 text-[length:var(--text-xs)] text-danger"
              >
                {run.error}
              </p>
            ) : null}
          </div>
        ) : null}
      </td>
      <td className="px-3 py-2">{run.durationLabel ?? (run.state === 'running' ? '…' : '—')}</td>
      <td className="px-3 py-2">
        {run.databaseBytes === null ? '—' : formatSize(run.databaseBytes)}
      </td>
      <td className="px-3 py-2">
        {run.attachmentsCopied === null
          ? '—'
          : `${run.attachmentsCopied}${run.attachmentsBytes === null ? '' : ` · ${formatSize(run.attachmentsBytes)}`}`}
      </td>
      <td className="px-3 py-2">{`g${run.passphraseGeneration}`}</td>
    </tr>
  );
}

/** The last 20 runs, newest first, as the server sends them. A pruned run is greyed. */
export function RunHistory({ runs, keepCount }: { runs: BackupRun[]; keepCount: number }) {
  return (
    <Panel title="History">
      {runs.length === 0 ? (
        <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">No backups yet.</p>
      ) : (
        <>
          <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
            The last 20 runs, newest first
          </p>
          <table className="w-full border-collapse text-left text-[length:var(--text-sm)]">
            <thead className="text-ink-muted">
              <tr>
                <th className="px-3 py-1 font-medium">Started</th>
                <th className="px-3 py-1 font-medium">Trigger</th>
                <th className="px-3 py-1 font-medium">Result</th>
                <th className="px-3 py-1 font-medium">Took</th>
                <th className="px-3 py-1 font-medium">Database</th>
                <th className="px-3 py-1 font-medium">Attachments</th>
                <th className="px-3 py-1 font-medium">Gen</th>
              </tr>
            </thead>
            <tbody>
              {runs.map((run) => (
                <RunRow key={run.id} run={run} />
              ))}
            </tbody>
          </table>
          <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
            {`A pruned backup was deleted from the bucket to keep the newest ${keepCount}; its row stays here.`}
          </p>
        </>
      )}
    </Panel>
  );
}
