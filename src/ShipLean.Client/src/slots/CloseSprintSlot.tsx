import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import { pagesTestIds } from '../features/pages/testIds';
import { CloseSprint } from '../features/sprints/end/CloseSprint';

export interface CloseSprintSlotProps {
  project: ProjectDto;
  sprint: SprintDto;
  // Opened from elsewhere (the touch sprint header's ⋯): the slot then shows no button of its own.
  open?: boolean;
  onClose?: () => void;
}

export function CloseSprintSlot({ project, sprint, open, onClose }: CloseSprintSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotCloseSprint}>
      <CloseSprint
        key={sprint.id}
        project={project}
        sprint={sprint}
        open={open}
        onClose={onClose}
      />
    </div>
  );
}
