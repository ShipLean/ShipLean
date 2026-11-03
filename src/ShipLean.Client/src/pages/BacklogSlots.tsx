import type { ProjectDto } from '../api/projects';
import type { Level } from '../routes';
import { LevelBacklogSlot } from '../slots/LevelBacklogSlot';
import { ListModeSlot } from '../slots/ListModeSlot';
import { PlanningModeSlot } from '../slots/PlanningModeSlot';
import { TreeModeSlot } from '../slots/TreeModeSlot';
import { backlogSlots } from './backlogSlots';
import { useRouteQuery } from './useRouteQuery';

// The slots of a backlog page.
export function BacklogSlots({ project, level }: { project: ProjectDto; level: Level }) {
  const slots = backlogSlots(project, level, useRouteQuery().mode ?? 'list');
  return (
    <>
      {slots.levelBacklog && <LevelBacklogSlot {...slots.levelBacklog} />}
      {slots.listMode && <ListModeSlot {...slots.listMode} />}
      {slots.planningMode && <PlanningModeSlot {...slots.planningMode} />}
      {slots.treeMode && <TreeModeSlot {...slots.treeMode} />}
    </>
  );
}
