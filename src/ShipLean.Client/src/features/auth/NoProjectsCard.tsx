import { Link } from 'react-router-dom';
import type { MeDto } from '../../api/me';
import { routePath } from '../../routes';
import { SignOutInstead } from './SignOutInstead';
import { authTestIds } from './testIds';

// Signed in, but in no project yet.
export function NoProjectsCard({
  me,
  instanceName,
}: {
  me: MeDto;
  instanceName: string | undefined;
}) {
  return (
    <div
      data-testid={authTestIds.noProjects}
      className="flex w-[min(460px,100%)] flex-col rounded-frame border border-edge bg-panel shadow-panel"
    >
      <h1 className="m-0 border-b border-edge px-4 py-3 text-[length:var(--text-md)] font-semibold">
        Welcome to ShipLean
        {instanceName ? (
          <small className="mt-0.5 block text-[length:var(--text-xs)] font-normal text-ink-muted">
            {instanceName}
          </small>
        ) : null}
      </h1>
      <div className="grid justify-items-center gap-2 px-4 py-6 text-center">
        <span className="text-[length:var(--text-sm)]">
          Signed in as <b>{me.name}</b>
        </span>
        <h2 className="m-0 text-[length:var(--text-md)]">You're not in any projects yet</h2>
        <p className="m-0 max-w-[360px] text-[length:var(--text-sm)] text-ink-muted">
          A project admin adds you from Project settings → Members. Once they do, the project opens
          here.
        </p>
        {me.canCreateProjects ? (
          <div className="flex justify-center gap-2">
            <Link
              data-testid={authTestIds.newProject}
              className="btn-signal"
              to={routePath('newProject', {})}
            >
              + New project
            </Link>
            <Link
              data-testid={authTestIds.importFromJira}
              className="btn-ghost"
              to={routePath('importStart', {})}
            >
              Import from Jira
            </Link>
          </div>
        ) : null}
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          Or{' '}
          <Link className="text-signal underline" to={routePath('profile', {})}>
            set up your profile
          </Link>{' '}
          while you wait.
        </span>
      </div>
      <footer className="flex items-center gap-2 rounded-b-frame border-t border-edge bg-canvas px-4 py-3">
        <span className="flex-1 text-[length:var(--text-xs)] text-ink-muted">
          Ask your ShipLean admin to add you.
        </span>
        <SignOutInstead label="Sign out" className="btn-ghost" />
      </footer>
    </div>
  );
}
