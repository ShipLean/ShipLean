import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { pagesTestIds } from '../features/pages/testIds';
import { NewSprintRail } from '../features/sprints/newSprint/NewSprintRail';

export interface NewSprintRailSlotProps {
  project: ProjectDto;
  sprints: SprintDto[];
}

export function NewSprintRailSlot({ project }: NewSprintRailSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotNewSprintRail} className="mt-4">
      <NewSprintRail projectId={project.id} />
    </div>
  );
}
