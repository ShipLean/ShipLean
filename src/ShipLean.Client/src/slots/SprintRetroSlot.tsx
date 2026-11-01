import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import { pagesTestIds } from '../features/pages/testIds';
import { SprintRetroPanel } from '../features/retros/sprint/SprintRetroPanel';

export interface SprintRetroSlotProps {
  project: ProjectDto;
  sprint: SprintDto;
  sprints: SprintDto[];
}

export function SprintRetroSlot({ project, sprint }: SprintRetroSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotSprintRetro}>
      <SprintRetroPanel key={sprint.id} project={project} sprint={sprint} />
    </div>
  );
}
