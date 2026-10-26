import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import type { SprintHeaderProps } from '../features/sprintHeader/SprintHeader';
import type { CapacitySlotProps } from '../slots/CapacitySlot';

// One sprint's capacity under the full sprint header.
export function capacitySlots(
  project: ProjectDto,
  sprint: SprintDto,
  sprints: SprintDto[],
): { header: SprintHeaderProps; capacity: CapacitySlotProps } {
  return {
    header: { variant: 'full', project, sprint, sprints, tab: 'capacity' },
    capacity: { project, sprint, sprints },
  };
}
