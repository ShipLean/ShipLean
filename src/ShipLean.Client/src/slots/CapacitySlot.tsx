import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import { pagesTestIds } from '../features/pages/testIds';
import { CapacityPanel } from '../features/sprints/capacity/CapacityPanel';

export interface CapacitySlotProps {
  project: ProjectDto;
  sprint: SprintDto;
  sprints: SprintDto[];
}

export function CapacitySlot(props: CapacitySlotProps) {
  return (
    <div data-testid={pagesTestIds.slotCapacity}>
      <CapacityPanel key={props.sprint.id} {...props} />
    </div>
  );
}
