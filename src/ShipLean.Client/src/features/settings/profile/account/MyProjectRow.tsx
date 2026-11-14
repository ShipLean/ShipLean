import type { ProjectSummaryDto } from '@shiplean/core/api/projects';
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
      className="flex items-center gap-3 border-b border-edge/60 px-3 py-2 text-[length:var(--text-sm)] touch:grid touch:grid-cols-[52px_minmax(0,1fr)_auto] touch:gap-x-2 touch:gap-y-0"
    >
      <span
        data-testid={settingsTestIds.myProjectKey}
        className="w-16 font-semibold text-ink touch:row-span-2 touch:w-auto"
      >
        {project.key}
      </span>
      <span className="min-w-0 flex-1 truncate touch:col-start-2 touch:text-[length:var(--text-base)] touch:font-medium">
        {project.name}
      </span>
      <span
        data-testid={settingsTestIds.myProjectRole}
        className="w-20 text-ink-muted touch:col-start-2 touch:row-start-2 touch:w-auto"
      >
        {ROLE_LABELS[project.role]}
      </span>
      <button
        type="button"
        data-testid={settingsTestIds.myProjectLeave}
        className="btn-ghost touch:h-[var(--touch-target)] touch:col-start-3 touch:row-span-2 touch:row-start-1"
        disabled={confirming}
        onClick={onLeave}
      >
        Leave
      </button>
    </li>
  );
}
