import { Link } from 'react-router-dom';
import type { JiraProjectDto } from '../../../api/import';
import type { ProjectDto } from '../../../api/projects';
import { routePath } from '../../../routes';
import { importTestIds as ids } from '../testIds';
import { targetOf } from './stepModel';

// Step 3: where the import goes. The project it is opened in, when that is empty (or this is a re-run into it).
export function TargetStep({ project, jira }: { project: ProjectDto; jira: JiraProjectDto }) {
  const target = targetOf(project, jira);
  return (
    <section
      data-testid={ids.targetStep}
      className="panel flex flex-col gap-[var(--space-3)] p-[var(--space-4)]"
    >
      <h2 className="font-semibold">
        Target project{' '}
        <span className="font-normal text-ink-muted">
          Importing {jira.key} · {jira.name}
        </span>
      </h2>
      {target.kind === 'ready' ? (
        <>
          <p data-testid={ids.targetFixed}>
            Imports into <b className="font-mono">{project.key}</b> · {project.name}.
            {target.rerun ? (
              <span data-testid={ids.targetRerun}>
                {' '}
                {jira.key} was imported here before: this is a re-run, items are matched and
                updated.
              </span>
            ) : null}
          </p>
          <p data-testid={ids.keyExample} className="font-mono text-[length:var(--text-sm)]">
            {jira.key}-123 → {project.key}-123
          </p>
        </>
      ) : null}
      {target.kind === 'nonEmpty' ? (
        <p data-testid={ids.targetNonEmpty}>
          {project.key} already has work items, so it cannot be the target of a new import.{' '}
          <Link data-testid={ids.targetOther} to={routePath('importStart', {})}>
            Import into a new project instead
          </Link>
        </p>
      ) : null}
      {target.kind === 'offsite' ? (
        <p data-testid={ids.targetOffsite}>
          {jira.key} was imported into {target.projectKey}.{' '}
          <Link
            data-testid={ids.targetOther}
            to={routePath('jiraImport', { key: target.projectKey })}
          >
            Re-run it in {target.projectKey}
          </Link>
        </p>
      ) : null}
    </section>
  );
}
