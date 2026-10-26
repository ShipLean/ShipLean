import { settingsTestIds } from '../testIds';
import type { InstanceProjectDto } from '../../../api/projects';

/** One flagged project: nobody active can change its settings or members until an admin is made. */
export function NoAdminBanner({
  project,
  joining,
  onJoin,
}: {
  project: InstanceProjectDto;
  joining: boolean;
  onJoin(): void;
}) {
  return (
    <div
      data-testid={settingsTestIds.projectsBanner}
      data-project-key={project.key}
      className="flex flex-wrap items-center gap-2 rounded-panel border border-edge bg-panel p-2 text-[length:var(--text-sm)] text-ink"
    >
      <b>
        {project.key} · {project.name} has no active admin.
      </b>
      <span className="flex-1 text-ink-muted">
        Nobody can change its settings or members until someone is made admin.
      </span>
      <button
        type="button"
        data-testid={settingsTestIds.projectsBannerJoin}
        className="btn-ghost"
        disabled={joining}
        onClick={onJoin}
      >
        Join as admin
      </button>
    </div>
  );
}
