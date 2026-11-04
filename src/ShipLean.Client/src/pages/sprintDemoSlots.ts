import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import type { SprintHeaderProps } from '../features/sprintHeader/SprintHeader';
import type { SprintDemoSlotProps } from '../slots/SprintDemoSlot';

// One sprint's Demo tab under the full sprint header.
export function sprintDemoSlots(
  project: ProjectDto,
  sprint: SprintDto,
  sprints: SprintDto[],
): { header: SprintHeaderProps; sprintDemo: SprintDemoSlotProps } {
  return {
    header: { variant: 'full', project, sprint, sprints, tab: 'demo' },
    sprintDemo: { project, sprint, sprints },
  };
}
