import { useState } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { pagesTestIds } from '../features/pages/testIds';
import { NewSprintRail } from '../features/sprints/newSprint/NewSprintRail';
import { sprintsTestIds } from '../features/sprints/testIds';
import { useTouchShell } from '../touch/useTouchShell';

export interface NewSprintRailSlotProps {
  project: ProjectDto;
  sprints: SprintDto[];
}

/** The New sprint rail beside the list; at 900px and below a one-row toolbar whose + New sprint opens the form full screen. */
export function NewSprintRailSlot({ project }: NewSprintRailSlotProps) {
  const { touch } = useTouchShell();
  const [open, setOpen] = useState(false);
  if (touch) {
    return (
      <div data-testid={pagesTestIds.slotNewSprintRail} className="mb-3 flex items-center">
        <div data-testid={sprintsTestIds.newSprintToolbar} className="flex w-full items-center">
          <span className="flex-1" />
          <button
            type="button"
            data-testid={sprintsTestIds.newSprintOpen}
            className="btn-signal h-[var(--touch-target)]"
            onClick={() => setOpen(true)}
          >
            + New sprint
          </button>
        </div>
        {open ? <NewSprintRail projectId={project.id} onClose={() => setOpen(false)} /> : null}
      </div>
    );
  }
  return (
    <div data-testid={pagesTestIds.slotNewSprintRail} className="mt-4">
      <NewSprintRail projectId={project.id} />
    </div>
  );
}
