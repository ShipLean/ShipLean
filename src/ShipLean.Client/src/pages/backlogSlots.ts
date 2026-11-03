import type { ProjectDto } from '../api/projects';
import type { BacklogMode, Level } from '../routes';
import type { LevelBacklogSlotProps } from '../slots/LevelBacklogSlot';
import type { ListModeSlotProps } from '../slots/ListModeSlot';
import type { PlanningModeSlotProps } from '../slots/PlanningModeSlot';
import type { TreeModeSlotProps } from '../slots/TreeModeSlot';

export interface BacklogSlots {
  listMode?: ListModeSlotProps;
  planningMode?: PlanningModeSlotProps;
  treeMode?: TreeModeSlotProps;
  levelBacklog?: LevelBacklogSlotProps;
}

// Which slots a backlog page mounts, and with what. Stories show the mode ?mode= names, Features and Epics the one
// list.
export function backlogSlots(project: ProjectDto, level: Level, mode: BacklogMode): BacklogSlots {
  const props = { project, level, mode };
  return {
    levelBacklog: level !== 'stories' ? props : undefined,
    listMode: level === 'stories' && mode === 'list' ? props : undefined,
    planningMode: level === 'stories' && mode === 'planning' ? props : undefined,
    treeMode: level === 'stories' && mode === 'tree' ? props : undefined,
  };
}
