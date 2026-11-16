import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import type { SprintHeaderProps } from '../features/sprintHeader/SprintHeader';
import type { SprintRetroSlotProps } from '../slots/SprintRetroSlot';

// One sprint's Retro tab under the full sprint header.
export function sprintRetroSlots(
  project: ProjectDto,
  sprint: SprintDto,
  sprints: SprintDto[],
): { sprintRetro: SprintRetroSlotProps } {
  const header: SprintHeaderProps = { variant: 'full', project, sprint, sprints, tab: 'retro' };
  return { sprintRetro: { project, sprint, header } };
}
