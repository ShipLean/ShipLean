import { useQuery } from '@tanstack/react-query';
import type { ProjectDto } from '../api/projects';
import type { Level } from '../routes';
import { LevelBacklogSlot } from '../slots/LevelBacklogSlot';
import { ListModeSlot } from '../slots/ListModeSlot';
import { PlanningModeSlot } from '../slots/PlanningModeSlot';
import { SprintEndPromptSlot } from '../slots/SprintEndPromptSlot';
import { TreeModeSlot } from '../slots/TreeModeSlot';
import { backlogSlots } from './backlogSlots';
import { sprintsQuery } from './projectQueries';
import { useRouteQuery } from './useRouteQuery';

// The slots of a backlog page. Reads the sprints itself so that only the sprint end prompt waits for them.
export function BacklogSlots({ project, level }: { project: ProjectDto; level: Level }) {
  const sprints = useQuery(sprintsQuery(project.id));
  const slots = backlogSlots(project, level, useRouteQuery().mode ?? 'list', sprints);
  return (
    <>
      {slots.sprintEndPrompt && <SprintEndPromptSlot {...slots.sprintEndPrompt} />}
      {slots.levelBacklog && <LevelBacklogSlot {...slots.levelBacklog} />}
      {slots.listMode && <ListModeSlot {...slots.listMode} />}
      {slots.planningMode && <PlanningModeSlot {...slots.planningMode} />}
      {slots.treeMode && <TreeModeSlot {...slots.treeMode} />}
    </>
  );
}
