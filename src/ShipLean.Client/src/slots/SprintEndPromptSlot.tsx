import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import { pagesTestIds } from '../features/pages/testIds';
import { SprintEnd } from '../features/sprints/end/SprintEnd';

export interface SprintEndPromptSlotProps {
  project: ProjectDto;
  sprints: SprintDto[];
}

export function SprintEndPromptSlot({ project, sprints }: SprintEndPromptSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotSprintEndPrompt}>
      <SprintEnd project={project} sprints={sprints} />
    </div>
  );
}
