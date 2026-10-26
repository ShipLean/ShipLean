import type { ProjectDto } from '../api/projects';
import type { Level, BacklogMode } from '../routes';
import { PlanningMode } from '../features/backlog/planning/PlanningMode';
import { pagesTestIds } from '../features/pages/testIds';

export interface PlanningModeSlotProps {
  project: ProjectDto;
  level: Level;
  mode: BacklogMode;
}

export function PlanningModeSlot({ project }: PlanningModeSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotPlanningMode} className="flex min-h-0 flex-1 flex-col">
      <PlanningMode project={project} />
    </div>
  );
}
