import type { UseQueryResult } from '@tanstack/react-query';
import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import type { BacklogMode, Level } from '../routes';
import type { LevelBacklogSlotProps } from '../slots/LevelBacklogSlot';
import type { ListModeSlotProps } from '../slots/ListModeSlot';
import type { PlanningModeSlotProps } from '../slots/PlanningModeSlot';
import type { SprintEndPromptSlotProps } from '../slots/SprintEndPromptSlot';
import type { TreeModeSlotProps } from '../slots/TreeModeSlot';

export interface BacklogSlots {
  sprintEndPrompt?: SprintEndPromptSlotProps;
  listMode?: ListModeSlotProps;
  planningMode?: PlanningModeSlotProps;
  treeMode?: TreeModeSlotProps;
  levelBacklog?: LevelBacklogSlotProps;
}

// Which slots a backlog page mounts, and with what. Stories show the mode ?mode= names, Features and Epics the one
// list. The sprint end prompt waits for the sprints; if they cannot be read it gets none, and the page is still a
// page.
export function backlogSlots(
  project: ProjectDto,
  level: Level,
  mode: BacklogMode,
  sprints: Pick<UseQueryResult<SprintDto[]>, 'data' | 'isError'>,
): BacklogSlots {
  const props = { project, level, mode };
  const sprintsOrNone = sprints.isError ? [] : sprints.data;
  return {
    sprintEndPrompt: sprintsOrNone === undefined ? undefined : { project, sprints: sprintsOrNone },
    levelBacklog: level !== 'stories' ? props : undefined,
    listMode: level === 'stories' && mode === 'list' ? props : undefined,
    planningMode: level === 'stories' && mode === 'planning' ? props : undefined,
    treeMode: level === 'stories' && mode === 'tree' ? props : undefined,
  };
}
