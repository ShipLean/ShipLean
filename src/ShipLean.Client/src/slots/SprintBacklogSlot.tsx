import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import { pagesTestIds } from '../features/pages/testIds';
import { SprintBacklog } from '../features/sprints/backlog/SprintBacklog';

export interface SprintBacklogSlotProps {
  project: ProjectDto;
  sprint: SprintDto;
  sprints: SprintDto[];
}

export function SprintBacklogSlot({ project, sprint }: SprintBacklogSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotSprintBacklog}>
      <SprintBacklog project={project} sprint={sprint} />
    </div>
  );
}
