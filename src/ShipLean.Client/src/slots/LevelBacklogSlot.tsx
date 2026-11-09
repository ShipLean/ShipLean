import type { ProjectDto } from '@shiplean/core/api/projects';
import type { Level, BacklogMode } from '../routes';
import { LevelBacklog } from '../features/backlog/levels/LevelBacklog';
import { pagesTestIds } from '../features/pages/testIds';

export interface LevelBacklogSlotProps {
  project: ProjectDto;
  level: Level;
  mode: BacklogMode;
}

// Features take ?mode=tree (any other mode is List); Epics are always a list. Each mode starts fresh, as the Stories modes do.
export function LevelBacklogSlot({ project, level, mode }: LevelBacklogSlotProps) {
  const levelMode = level === 'features' && mode === 'tree' ? 'tree' : 'list';
  return (
    <div data-testid={pagesTestIds.slotLevelBacklog} className="flex min-h-0 flex-1 flex-col">
      {level === 'stories' ? null : (
        <LevelBacklog
          key={`${level}-${levelMode}`}
          project={project}
          level={level}
          mode={levelMode}
        />
      )}
    </div>
  );
}
