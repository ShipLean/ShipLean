import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { routePath } from '../../../routes';
import { sprintsTestIds as ids } from '../testIds';

/**
 * The All sprints head at 900px and below (SHIP-1807 P1, P7): the project and its sprint count, Plan sprints and the
 * New sprint button the page passes in. With no sprints it is the line alone: the empty panel holds + New sprint.
 */
export function SprintsTouchHead({
  project,
  count,
  children,
}: {
  project: ProjectDto;
  count: number;
  children?: ReactNode;
}) {
  return (
    // Top-aligned: the New sprint slot carries its own bottom margin, which is the gap under the row.
    <div
      data-testid={ids.listHead}
      className={`flex min-w-0 items-start gap-2 ${count === 0 ? 'mb-2' : ''}`}
    >
      <span className="flex min-h-[var(--touch-target)] min-w-0 flex-1 items-center truncate text-[length:var(--text-sm)] text-ink-muted">
        {project.name} ·{' '}
        {count === 0 ? 'no sprints' : `${count} ${count === 1 ? 'sprint' : 'sprints'}`}
      </span>
      {count === 0 ? null : (
        <>
          <Link
            data-testid={ids.planSprints}
            className="btn-ghost h-[var(--touch-target)] flex-none no-underline"
            to={routePath('storiesBacklog', { key: project.key }, { mode: 'planning' })}
          >
            Plan sprints
          </Link>
          {children}
        </>
      )}
    </div>
  );
}
