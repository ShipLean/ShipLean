import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import type { SprintHeaderProps } from '../features/sprintHeader/SprintHeader';
import type { SprintRetroSlotProps } from '../slots/SprintRetroSlot';

// One sprint's Retro tab under the full sprint header.
export function sprintRetroSlots(
  project: ProjectDto,
  sprint: SprintDto,
  sprints: SprintDto[],
): { header: SprintHeaderProps; sprintRetro: SprintRetroSlotProps } {
  return {
    header: { variant: 'full', project, sprint, sprints, tab: 'retro' },
    sprintRetro: { project, sprint, sprints },
  };
}
