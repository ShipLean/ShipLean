import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import { pagesTestIds } from '../features/pages/testIds';
import { SprintsList } from '../features/sprints/list/SprintsList';

export interface SprintsListSlotProps {
  project: ProjectDto;
  sprints: SprintDto[];
}

export function SprintsListSlot({ project, sprints }: SprintsListSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotSprintsList}>
      <SprintsList project={project} sprints={sprints} />
    </div>
  );
}
