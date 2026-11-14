import { Link } from 'react-router-dom';
import type { MeDto } from '@shiplean/core/api/me';
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
      className="flex w-[min(460px,100%)] flex-col rounded-frame border border-edge bg-panel shadow-panel touch:w-[min(440px,100%)]"
    >
      <h1 className="m-0 border-b border-edge px-4 py-3 text-[length:var(--text-md)] font-semibold touch:text-[length:var(--text-lg)]">
        Welcome to ShipLean
        {instanceName ? (
          <small className="mt-0.5 block text-[length:var(--text-xs)] font-normal text-ink-muted">
            {instanceName}
          </small>
        ) : null}
      </h1>
      <div className="grid justify-items-center gap-2 px-4 py-6 text-center touch:justify-items-stretch touch:p-4">
        <span className="text-[length:var(--text-sm)]">
          Signed in as <b>{me.name}</b>
        </span>
        <h2 className="m-0 text-[length:var(--text-md)]">You're not in any projects yet</h2>
        <p className="m-0 max-w-[360px] text-[length:var(--text-sm)] text-ink-muted">
          A project admin adds you from Project settings → Members. Once they do, the project opens
          here.
        </p>
        {me.canCreateProjects ? (
          <div className="flex justify-center gap-2 touch:flex-col">
            <Link
              data-testid={authTestIds.newProject}
              className="btn-signal touch:h-10 touch:w-full"
              to={routePath('newProject', {})}
            >
              + New project
            </Link>
            <Link
              data-testid={authTestIds.importFromJira}
              className="btn-ghost touch:h-10 touch:w-full"
              to={routePath('importStart', {})}
            >
              Import from Jira
            </Link>
          </div>
        ) : null}
        <span className="text-[length:var(--text-xs)] text-ink-muted touch:text-center">
          Or{' '}
          <Link
            className="text-signal underline touch:inline-flex touch:h-8 touch:items-center"
            to={routePath('profile', {})}
          >
            set up your profile
          </Link>{' '}
          while you wait.
        </span>
      </div>
      <footer className="flex items-center gap-2 rounded-b-frame border-t border-edge bg-canvas px-4 py-3 touch:flex-col touch:items-stretch">
        <span className="flex-1 text-[length:var(--text-xs)] text-ink-muted touch:text-center">
          Ask your ShipLean admin to add you.
        </span>
        <SignOutInstead label="Sign out" className="btn-ghost touch:h-8" />
      </footer>
    </div>
  );
}
