import type { ProjectDto } from '../api/projects';
import type { Level, BacklogMode } from '../routes';
import { TreeMode } from '../features/backlog/tree/TreeMode';
import { pagesTestIds } from '../features/pages/testIds';

export interface TreeModeSlotProps {
  project: ProjectDto;
  level: Level;
  mode: BacklogMode;
}

export function TreeModeSlot({ project }: TreeModeSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotTreeMode} className="flex min-h-0 flex-1 flex-col">
      <TreeMode project={project} />
    </div>
  );
}
