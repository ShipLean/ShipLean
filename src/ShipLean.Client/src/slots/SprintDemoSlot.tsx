import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import { SprintDemoPanel } from '../features/demos/SprintDemoPanel';
import { pagesTestIds } from '../features/pages/testIds';

export interface SprintDemoSlotProps {
  project: ProjectDto;
  sprint: SprintDto;
  sprints: SprintDto[];
}

export function SprintDemoSlot({ project, sprint }: SprintDemoSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotSprintDemo}>
      <SprintDemoPanel key={sprint.id} project={project} sprint={sprint} />
    </div>
  );
}
