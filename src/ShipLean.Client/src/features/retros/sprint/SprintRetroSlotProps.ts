import type { ProjectDto } from '../../../api/projects';
import type { SprintRetroDto } from '../../../api/retros';
import type { SprintDto } from '../../../api/sprints';

// What every part of the sprint Retro tab is given: the project, the sprint, and the tab's loaded data. `decide` and
// `point` come from the tab's address (sprintRetroAddress.ts); they are optional so a part can be mounted without them.
export interface SprintRetroSlotProps {
  project: ProjectDto;
  sprint: SprintDto;
  retro: SprintRetroDto;
  /** True when the address says `decide=open`. */
  decide?: boolean;
  /** The point the address names, to highlight. */
  point?: string;
}
