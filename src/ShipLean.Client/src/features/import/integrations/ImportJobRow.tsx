import { Link } from 'react-router-dom';
import type { ImportJobDto } from '../../../api/import';
import { routePath } from '../../../routes';
import { importTestIds as ids } from '../testIds';

export function ImportJobRow({ job }: { job: ImportJobDto }) {
  return (
    <li
      data-testid={ids.jobRow}
      data-status={job.status}
      className="flex items-center gap-[var(--space-3)] border-t border-edge py-2 text-[length:var(--text-sm)]"
    >
      <span className="font-mono">
        {job.jiraProjectKey} → {job.projectKey}
      </span>
      <span className="text-ink-muted">{job.status}</span>
      <span className="text-ink-muted">
        {job.finishedLabel ?? job.startedLabel ?? 'Not started'}
      </span>
      <Link
        data-testid={ids.jobLink}
        className="ml-auto"
        to={`${routePath('jiraImport', { key: job.projectKey })}?job=${encodeURIComponent(job.id)}`}
      >
        Open the import
      </Link>
    </li>
  );
}
