import type { ProjectDto } from '../../../api/projects';
import type { SprintRetroDto } from '../../../api/retros';
import type { SprintDto } from '../../../api/sprints';

// What every part of the sprint Retro tab is given: the project, the sprint, and the tab's loaded data.
export interface SprintRetroSlotProps {
  project: ProjectDto;
  sprint: SprintDto;
  retro: SprintRetroDto;
}
