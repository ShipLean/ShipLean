import type { ProjectDto } from '../api/projects';
import type { Level, BacklogMode } from '../routes';
import { ListMode } from '../features/backlog/list/ListMode';
import { pagesTestIds } from '../features/pages/testIds';

export interface ListModeSlotProps {
  project: ProjectDto;
  level: Level;
  mode: BacklogMode;
}

export function ListModeSlot({ project }: ListModeSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotListMode} className="flex min-h-0 flex-1 flex-col">
      <ListMode project={project} />
    </div>
  );
}
