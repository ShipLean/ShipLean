import { Link } from 'react-router-dom';
import { routePath } from '../../../routes';
import { backlogTestIds as ids } from '../testIds';

// The first-run state of a project with no work items.
export function EmptyBacklog({
  projectKey,
  onCreate,
  noSprints,
}: {
  projectKey: string;
  onCreate: () => void;
  // True once the project's sprints have loaded and there are none.
  noSprints: boolean;
}) {
  return (
    <section
      data-testid={ids.empty}
      className="grid flex-1 place-content-center justify-items-center gap-[var(--space-2)] bg-panel p-[var(--space-5)] text-center"
    >
      <h2 className="m-0 text-[length:var(--text-md)] font-semibold">No user stories yet</h2>
      <p className="m-0 max-w-[420px] text-[length:var(--text-sm)] text-ink-muted">
        Add the first one and it gets the key {projectKey}-1. Or bring a project over from Jira:
        issue numbers are kept.
      </p>
      <div className="flex gap-[var(--space-2)]">
        <button
          type="button"
          data-testid={ids.emptyCreate}
          className="btn-signal"
          onClick={onCreate}
        >
          Create your first story
        </button>
        <Link
          data-testid={ids.emptyImport}
          className="btn-ghost"
          to={routePath('jiraImport', { key: projectKey })}
        >
          Import from Jira
        </Link>
      </div>
      {noSprints ? (
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          No sprints yet either.{' '}
          <Link data-testid={ids.emptySprint} to={routePath('sprints', { key: projectKey })}>
            Create Sprint 1
          </Link>
        </span>
      ) : null}
    </section>
  );
}
