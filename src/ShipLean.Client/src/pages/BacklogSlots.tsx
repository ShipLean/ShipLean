import type { ProjectDto } from '../api/projects';
import { TouchSelectModeProvider } from '../features/backlog/touch/selectMode';
import type { Level } from '../routes';
import { LevelBacklogSlot } from '../slots/LevelBacklogSlot';
import { ListModeSlot } from '../slots/ListModeSlot';
import { PlanningModeSlot } from '../slots/PlanningModeSlot';
import { TreeModeSlot } from '../slots/TreeModeSlot';
import { backlogSlots } from './backlogSlots';
import { useRouteQuery } from './useRouteQuery';

// The slots of a backlog page, under the touch Select mode they share.
export function BacklogSlots({ project, level }: { project: ProjectDto; level: Level }) {
  const slots = backlogSlots(project, level, useRouteQuery().mode ?? 'list');
  return (
    <TouchSelectModeProvider>
      {slots.levelBacklog && <LevelBacklogSlot {...slots.levelBacklog} />}
      {slots.listMode && <ListModeSlot {...slots.listMode} />}
      {slots.planningMode && <PlanningModeSlot {...slots.planningMode} />}
      {slots.treeMode && <TreeModeSlot {...slots.treeMode} />}
    </TouchSelectModeProvider>
  );
}
