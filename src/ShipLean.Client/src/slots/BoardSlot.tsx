import type { ProjectDto } from '../api/projects';
import type { Level } from '../routes';
import { BoardView } from '../features/board/grid/BoardView';
import { pagesTestIds } from '../features/pages/testIds';

export interface BoardSlotProps {
  project: ProjectDto;
  level: Level;
}

export function BoardSlot({ project, level }: BoardSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotBoard} className="flex min-h-0 flex-1 flex-col">
      <BoardView project={project} level={level} />
    </div>
  );
}
