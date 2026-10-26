import type { ProjectDto } from '../api/projects';
import { pagesTestIds } from '../features/pages/testIds';
import { TasksSection } from '../features/workItem/tasks/TasksSection';

export interface TasksSlotProps {
  project: ProjectDto;
  itemKey: string;
}

// The work item's child tasks. They are work items of their own and save at once, not with the form.
export function TasksSlot({ project, itemKey }: TasksSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotTasks}>
      <TasksSection project={project} itemKey={itemKey} />
    </div>
  );
}
