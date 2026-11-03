import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import { pagesTestIds } from '../features/pages/testIds';
import { CloseSprint } from '../features/sprints/end/CloseSprint';

export interface CloseSprintSlotProps {
  project: ProjectDto;
  sprint: SprintDto;
}

export function CloseSprintSlot({ project, sprint }: CloseSprintSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotCloseSprint}>
      <CloseSprint key={sprint.id} project={project} sprint={sprint} />
    </div>
  );
}
