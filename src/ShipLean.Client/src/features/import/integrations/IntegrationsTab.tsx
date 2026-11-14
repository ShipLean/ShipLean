import { Link } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { routePath } from '../../../routes';
import { ConnectionPanel } from '../connection/ConnectionPanel';
import { importTestIds as ids } from '../testIds';
import { ImportJobsList } from './ImportJobsList';

// At touch widths the connection form (shared with the import stepper, so not edited here) gets 32px buttons, and Test
// connection fills the row.
const connectionTouch =
  'touch:[&_button]:min-h-[var(--touch-target)] touch:[&_button[data-testid=import-test]]:flex-1';

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
      <section
        className={`panel flex flex-col gap-[var(--space-3)] p-[var(--space-4)] ${connectionTouch}`}
      >
        <h2 className="font-semibold">Jira connection</h2>
        <ConnectionPanel projectId={project.id} />
      </section>
      <section className="panel flex flex-col gap-[var(--space-2)] p-[var(--space-4)]">
        <div className="flex items-center touch:flex-col touch:items-stretch touch:gap-[var(--space-2)]">
          <h2 className="font-semibold">Import jobs</h2>
          <Link
            data-testid={ids.openImport}
            className="btn-signal ml-auto touch:ml-0 touch:min-h-[var(--touch-target)] touch:w-full"
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
