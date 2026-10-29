import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import { pagesTestIds } from '../features/pages/testIds';
import { EditSprintRail } from '../features/sprints/editSprint/EditSprintRail';

export interface EditSprintRailSlotProps {
  project: ProjectDto;
  sprint: SprintDto;
  // The rail is finished: called after the PATCH succeeds (the sprint list is refreshed first) and on Cancel.
  onDone: () => void;
}

export function EditSprintRailSlot({ project, sprint, onDone }: EditSprintRailSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotEditSprintRail} className="max-w-[560px]">
      <EditSprintRail key={sprint.id} projectId={project.id} sprint={sprint} onDone={onDone} />
    </div>
  );
}
