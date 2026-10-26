import { Link } from 'react-router-dom';
import type { ProjectDto } from '../../../api/projects';
import { routePath } from '../../../routes';
import { ConnectionPanel } from '../connection/ConnectionPanel';
import { importTestIds as ids } from '../testIds';
import { ImportJobsList } from './ImportJobsList';

// Project settings, Integrations: the Jira connection and this project's import jobs. Both are project-admin reads.
export function IntegrationsTab({ project }: { project: ProjectDto }) {
  if (project.role !== 'admin') {
    return (
      <p data-testid={ids.integrationsAdminOnly} className="p-[var(--space-4)] text-ink-muted">
        Only project admins see the Jira connection and its import jobs.
      </p>
    );
  }
  return (
    <div
      data-testid={ids.integrationsTab}
      className="flex flex-col gap-[var(--space-4)] p-[var(--space-4)]"
    >
      <section className="panel flex flex-col gap-[var(--space-3)] p-[var(--space-4)]">
        <h2 className="font-semibold">Jira connection</h2>
        <ConnectionPanel projectId={project.id} />
      </section>
      <section className="panel flex flex-col gap-[var(--space-2)] p-[var(--space-4)]">
        <div className="flex items-center">
          <h2 className="font-semibold">Import jobs</h2>
          <Link
            data-testid={ids.openImport}
            className="btn-signal ml-auto"
            to={routePath('jiraImport', { key: project.key })}
          >
            Open Jira import
          </Link>
        </div>
        <ImportJobsList projectId={project.id} />
      </section>
    </div>
  );
}
