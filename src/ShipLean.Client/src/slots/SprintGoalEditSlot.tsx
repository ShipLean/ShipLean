import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import { pagesTestIds } from '../features/pages/testIds';
import { SprintGoalEditor } from '../features/sprints/goal/SprintGoalEditor';

export interface SprintGoalEditSlotProps {
  project: ProjectDto;
  sprint: SprintDto;
  sprints: SprintDto[];
  // The editor is finished: called after the goal PATCH succeeds (the slot has invalidated
  // ['projects', projectId, 'sprints'] first) and on Cancel or Escape.
  onDone: () => void;
}

export function SprintGoalEditSlot({ project, sprint, onDone }: SprintGoalEditSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotSprintGoalEdit}>
      <SprintGoalEditor project={project} sprint={sprint} onDone={onDone} />
    </div>
  );
}
