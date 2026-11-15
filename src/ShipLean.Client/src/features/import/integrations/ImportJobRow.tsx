import { Link } from 'react-router-dom';
import type { ImportJobDto } from '@shiplean/core/api/import';
import { routePath } from '../../../routes';
import { importTestIds as ids } from '../testIds';

export function ImportJobRow({ job }: { job: ImportJobDto }) {
  return (
    <li
      data-testid={ids.jobRow}
      data-status={job.status}
      className="flex items-center gap-[var(--space-3)] border-t border-edge py-2 text-[length:var(--text-sm)] touch:grid touch:grid-cols-[minmax(0,1fr)_auto] touch:gap-x-[var(--space-3)] touch:gap-y-1"
    >
      <span className="font-mono touch:min-w-0 touch:break-words">
        {job.jiraProjectKey} → {job.projectKey}
      </span>
      <span className="text-ink-muted">{job.status}</span>
      <span className="text-ink-muted">
        {job.finishedLabel ?? job.startedLabel ?? 'Not started'}
      </span>
      <Link
        data-testid={ids.jobLink}
        className="ml-auto touch:ml-0 touch:inline-flex touch:min-h-[var(--touch-target)] touch:items-center touch:justify-self-end"
        to={`${routePath('jiraImport', { key: job.projectKey })}?job=${encodeURIComponent(job.id)}`}
      >
        Open the import
      </Link>
    </li>
  );
}
