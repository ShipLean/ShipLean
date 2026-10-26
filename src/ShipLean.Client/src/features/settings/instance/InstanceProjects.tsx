import { useState } from 'react';
import { Link } from 'react-router-dom';
import type { InstanceProjectDto } from '../../../api/projects';
import { showToast } from '../../../components/toastStore';
import { routePath } from '../../../routes';
import { settingsTestIds } from '../testIds';
import { AccessState } from './AccessState';
import { useInstanceProjects, useJoinAsAdmin } from './instanceApi';
import { errorText } from './model';
import { NoAdminBanner } from './NoAdminBanner';
import { ProjectsTable } from './ProjectsTable';

/** The instance admin's Projects tab: every project on the install, including ones the admin isn't in. */
export function InstanceProjects() {
  const projects = useInstanceProjects();
  return (
    <AccessState pending={projects.isPending} error={projects.error}>
      {projects.data ? <ProjectsList projects={projects.data} /> : null}
    </AccessState>
  );
}

function ProjectsList({ projects }: { projects: InstanceProjectDto[] }) {
  const join = useJoinAsAdmin();
  const [joining, setJoining] = useState<ReadonlySet<string>>(new Set());
  const [joinError, setJoinError] = useState<string | null>(null);

  const track = (id: string, on: boolean) =>
    setJoining((ids) => {
      const next = new Set(ids);
      if (on) next.add(id);
      else next.delete(id);
      return next;
    });

  // mutateAsync, so each join settles on its own: two quick joins show both toasts, and each button has its own state.
  const onJoin = async (project: InstanceProjectDto) => {
    track(project.id, true);
    setJoinError(null);
    try {
      await join.mutateAsync(project.id);
      showToast(`You’re now an Admin of ${project.key}. It shows in its Members list`, {
        tone: 'ok',
      });
    } catch (error) {
      setJoinError(errorText(error));
    } finally {
      track(project.id, false);
    }
  };

  return (
    <section data-testid={settingsTestIds.projectsPanel} className="grid gap-3 p-4">
      {projects
        .filter((p) => !p.hasActiveAdmin)
        .map((p) => (
          <NoAdminBanner
            key={p.id}
            project={p}
            joining={joining.has(p.id)}
            onJoin={() => void onJoin(p)}
          />
        ))}
      <header className="flex items-center gap-2">
        <h2 className="m-0 text-[length:var(--text-md)] font-semibold text-ink">Projects</h2>
        <span
          data-testid={settingsTestIds.projectsCount}
          className="text-[length:var(--text-sm)] text-ink-muted"
        >
          {projects.length} on this install · keys are unique across all of them
        </span>
        <span className="flex-1" />
        <Link
          data-testid={settingsTestIds.projectsNew}
          className="btn-ghost"
          to={routePath('newProject', {})}
        >
          + New project
        </Link>
      </header>
      {joinError ? (
        <p
          role="alert"
          data-testid={settingsTestIds.projectsJoinError}
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          {joinError}
        </p>
      ) : null}
      <ProjectsTable projects={projects} joining={joining} onJoin={(p) => void onJoin(p)} />
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        Instance admins see every project here, but open one only by joining it. Joining adds you to
        its Members, where everyone in the project can see it.
      </p>
    </section>
  );
}
