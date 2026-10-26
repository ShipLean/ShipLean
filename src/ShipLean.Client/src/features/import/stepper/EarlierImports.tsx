import { Link } from 'react-router-dom';
import { routePath } from '../../../routes';
import { useAllImportJobs } from '../importApi';
import { importTestIds as ids } from '../testIds';

// The user's earlier imports, so a re-run goes into its project instead of a new, empty one.
export function EarlierImports() {
  const jobs = useAllImportJobs();
  const earlier = jobs.data?.filter((job) => job.projectId !== null) ?? [];
  if (earlier.length === 0) return null;
  return (
    <div data-testid={ids.earlierImports} className="flex flex-col gap-[var(--space-2)]">
      <h2 className="font-semibold">Earlier imports</h2>
      <ul className="m-0 list-none p-0 text-[length:var(--text-sm)]">
        {earlier.map((job) => (
          <li key={job.id} data-testid={ids.earlierImport} className="flex items-center gap-2 py-1">
            <span className="font-mono">
              {job.jiraProjectKey} → {job.projectKey}
            </span>
            <span className="text-ink-muted">{job.finishedLabel ?? job.startedLabel}</span>
            <Link className="ml-auto" to={routePath('jiraImport', { key: job.projectKey })}>
              Re-run in {job.projectKey}
            </Link>
          </li>
        ))}
      </ul>
      <p className="text-ink-muted">Or start a new project below.</p>
    </div>
  );
}
