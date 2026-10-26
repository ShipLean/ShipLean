import type { ProjectDto } from '../api/projects';
import type { Level, BacklogMode } from '../routes';
import { LevelBacklog } from '../features/backlog/levels/LevelBacklog';
import { pagesTestIds } from '../features/pages/testIds';

export interface LevelBacklogSlotProps {
  project: ProjectDto;
  level: Level;
  mode: BacklogMode;
}

export function LevelBacklogSlot({ project, level }: LevelBacklogSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotLevelBacklog} className="flex min-h-0 flex-1 flex-col">
      {level === 'stories' ? null : <LevelBacklog key={level} project={project} level={level} />}
    </div>
  );
}
