import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import type { SprintHeaderProps } from '../features/sprintHeader/SprintHeader';
import type { SprintBacklogSlotProps } from '../slots/SprintBacklogSlot';

// One sprint's backlog under the full sprint header.
export function sprintBacklogSlots(
  project: ProjectDto,
  sprint: SprintDto,
  sprints: SprintDto[],
): { header: SprintHeaderProps; sprintBacklog: SprintBacklogSlotProps } {
  return {
    header: { variant: 'full', project, sprint, sprints, tab: 'backlog' },
    sprintBacklog: { project, sprint, sprints },
  };
}
