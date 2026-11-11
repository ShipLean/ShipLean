import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import type { NewSprintRailSlotProps } from '../slots/NewSprintRailSlot';
import type { SprintsListSlotProps } from '../slots/SprintsListSlot';

export interface SprintsSlots {
  sprintsList: SprintsListSlotProps;
  newSprintRail: NewSprintRailSlotProps;
}

// All sprints: the list and the New sprint rail.
export function sprintsSlots(project: ProjectDto, sprints: SprintDto[]): SprintsSlots {
  return {
    sprintsList: { project, sprints },
    newSprintRail: { project, sprints },
  };
}
