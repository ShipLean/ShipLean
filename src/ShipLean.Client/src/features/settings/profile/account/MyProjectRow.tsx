import type { ProjectSummaryDto } from '../../../../api/projects';
import { settingsTestIds } from '../../testIds';

const ROLE_LABELS = { admin: 'Admin', member: 'Member' } as const;

/** One of your memberships, with Leave; asking first is the confirmation row below it. */
export function MyProjectRow({
  project,
  confirming,
  onLeave,
}: {
  project: ProjectSummaryDto;
  confirming: boolean;
  onLeave(): void;
}) {
  return (
    <li
      data-testid={settingsTestIds.myProjectRow}
      data-key={project.key}
      className="flex items-center gap-3 border-b border-edge/60 px-3 py-2 text-[length:var(--text-sm)]"
    >
      <span data-testid={settingsTestIds.myProjectKey} className="w-16 font-semibold text-ink">
        {project.key}
      </span>
      <span className="min-w-0 flex-1 truncate">{project.name}</span>
      <span data-testid={settingsTestIds.myProjectRole} className="w-20 text-ink-muted">
        {ROLE_LABELS[project.role]}
      </span>
      <button
        type="button"
        data-testid={settingsTestIds.myProjectLeave}
        className="btn-ghost"
        disabled={confirming}
        onClick={onLeave}
      >
        Leave
      </button>
    </li>
  );
}
