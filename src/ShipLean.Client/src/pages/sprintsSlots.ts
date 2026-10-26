import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import type { NewSprintRailSlotProps } from '../slots/NewSprintRailSlot';
import type { SprintEndPromptSlotProps } from '../slots/SprintEndPromptSlot';
import type { SprintsListSlotProps } from '../slots/SprintsListSlot';

export interface SprintsSlots {
  sprintEndPrompt: SprintEndPromptSlotProps;
  sprintsList: SprintsListSlotProps;
  newSprintRail: NewSprintRailSlotProps;
}

// All sprints: the list, the New sprint rail and the sprint end prompt.
export function sprintsSlots(project: ProjectDto, sprints: SprintDto[]): SprintsSlots {
  return {
    sprintEndPrompt: { project, sprints },
    sprintsList: { project, sprints },
    newSprintRail: { project, sprints },
  };
}
