import type { ProjectDto } from '../../../api/projects';
import type { Level } from '../../../routes';
import { useBoardOfLevel } from '../../boardSettings/useBoardOfLevel';
import { boardTestIds as ids } from '../testIds';
import { LoadedBoard } from './LoadedBoard';

// A level's board: finds its id in the project's boards, then draws it.
export function BoardView({ project, level }: { project: ProjectDto; level: Level }) {
  const { boardId, failed } = useBoardOfLevel(project.id, level);
  if (failed) {
    return (
      <div data-testid={ids.loadFailed} role="alert" className="p-4 text-alert">
        The board could not be loaded.
      </div>
    );
  }
  if (boardId === undefined) {
    return (
      <div data-testid={ids.loading} className="p-4 text-ink-muted">
        Loading the board…
      </div>
    );
  }
  return <LoadedBoard key={boardId} project={project} level={level} boardId={boardId} />;
}
