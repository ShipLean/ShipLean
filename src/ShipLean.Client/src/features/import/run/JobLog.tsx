import type { ImportJobDto } from '../../../api/import';
import { importTestIds as ids } from '../testIds';
import { useImportJobLog } from './runApi';

const marks = { info: '·', warning: '▲', error: '✕' } as const;

export function JobLog({ projectId, job }: { projectId: string; job: ImportJobDto }) {
  const log = useImportJobLog(projectId, job);
  return (
    <section className="panel flex flex-col gap-[var(--space-2)] p-[var(--space-4)]">
      <header className="flex items-center">
        <h2 className="font-semibold">Live log</h2>
        <span className="ml-auto text-[length:var(--text-xs)] text-ink-muted">
          {log.data?.length ?? 0} entries
        </span>
      </header>
      {log.isError ? (
        <p role="alert" className="text-alert">
          The log could not be loaded.
        </p>
      ) : (
        <ol
          data-testid={ids.runLog}
          aria-live="polite"
          className="m-0 flex max-h-80 list-none flex-col gap-1 overflow-auto p-0 font-mono text-[length:var(--text-xs)]"
        >
          {(log.data ?? []).map((line) => (
            <li
              key={line.id}
              data-testid={ids.runLogLine}
              data-level={line.level}
              className="flex gap-2"
            >
              <time className="text-ink-muted">{line.timeLabel}</time>
              <span aria-label={line.level}>{marks[line.level]}</span>
              <span>{line.message}</span>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
