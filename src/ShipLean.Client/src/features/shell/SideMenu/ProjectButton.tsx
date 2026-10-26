import type { Ref } from 'react';
import type { ProjectSummaryDto } from '../../../api/projects';
import { ProjectKeyBadge } from '../ProjectKeyBadge';
import { shellTestIds as ids } from '../testIds';

// The project at the top of the side menu; it opens the picker.
export function ProjectButton({
  ref,
  projectKey,
  project,
  open,
  onClick,
}: {
  ref: Ref<HTMLButtonElement>;
  projectKey: string | undefined;
  project: ProjectSummaryDto | undefined;
  open: boolean;
  onClick: () => void;
}) {
  return (
    <button
      ref={ref}
      type="button"
      data-testid={ids.projectButton}
      aria-haspopup="dialog"
      aria-expanded={open}
      title="Switch project (Ctrl+P)"
      className="mb-2 flex h-10 w-full items-center gap-2 rounded-panel border border-edge bg-panel px-2 text-left text-ink hover:border-signal aria-expanded:border-signal max-[900px]:mb-0 max-[900px]:w-auto"
      onClick={onClick}
    >
      {projectKey ? <ProjectKeyBadge current>{projectKey}</ProjectKeyBadge> : null}
      <span className="grid min-w-0 flex-1 leading-tight">
        <b className="truncate text-[length:var(--text-base)] font-semibold">
          {project?.name ?? projectKey ?? 'Choose a project'}
        </b>
        {project ? (
          <span className="text-[length:var(--text-2xs)] text-ink-muted">
            {project.memberCount} {project.memberCount === 1 ? 'member' : 'members'}
          </span>
        ) : null}
      </span>
      <span className="text-ink-muted" aria-hidden="true">
        ▾
      </span>
    </button>
  );
}
