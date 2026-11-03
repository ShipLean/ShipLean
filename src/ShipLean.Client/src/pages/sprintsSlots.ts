import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
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
