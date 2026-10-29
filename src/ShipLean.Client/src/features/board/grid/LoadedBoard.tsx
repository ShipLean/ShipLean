import { useState } from 'react';
import type { ProjectDto } from '../../../api/projects';
import type { Level } from '../../../routes';
import { ChildList } from '../children/ChildList';
import { useChildList } from '../children/useChildList';
import { useBoard, type BoardFilterQuery } from '../boardApi';
import { useCardDensity } from '../lanes/boardPreferences';
import { boardTestIds as ids } from '../testIds';
import { BoardGrid } from './BoardGrid';
import type { BoardContext } from './boardContext';
import { BoardToolbar } from './BoardToolbar';
import { useTeammateMoves } from '../realtime/useTeammateMoves';

// One board over GET /api/boards/{id}: the toolbar and the grid. The filter and the cursors of the Done cells
// the user expanded are kept here and sent back as `older`, so those cells stay expanded across refetches.
export function LoadedBoard({
  project,
  level,
  boardId,
}: {
  project: ProjectDto;
  level: Level;
  boardId: string;
}) {
  const { compact, setCompact } = useCardDensity(boardId);
  const [filter, setFilter] = useState<BoardFilterQuery>({});
  const [older, setOlder] = useState<string[]>([]);
  const { view, settingsChanges } = useBoard(project.id, boardId, { ...filter, older });
  const flashing = useTeammateMoves(project.id, view.data);
  const childList = useChildList();

  if (view.data === undefined) {
    return view.isError ? (
      <div data-testid={ids.loadFailed} role="alert" className="p-4 text-alert">
        The board could not be loaded.
      </div>
    ) : (
      <div data-testid={ids.loading} className="p-4 text-ink-muted">
        Loading the board…
      </div>
    );
  }
  const board = view.data;
  const context: BoardContext = {
    projectId: project.id,
    projectKey: project.key,
    boardId,
    level,
    compact,
    settings: board,
    flashing,
    onRollupClick: childList.openFor,
    onExpand: (cursor) =>
      setOlder((cursors) => (cursors.includes(cursor) ? cursors : [...cursors, cursor])),
  };
  return (
    <div data-testid={ids.view} data-level={level} className="flex min-h-0 flex-1 flex-col">
      <BoardToolbar
        view={board}
        projectId={project.id}
        boardId={boardId}
        level={level}
        compact={compact}
        onCompact={setCompact}
        filter={filter}
        onFilter={setFilter}
      />
      <div className="min-h-0 flex-1 overflow-auto bg-canvas">
        <BoardGrid view={board} context={context} headerFlashes={settingsChanges} />
      </div>
      {childList.open ? (
        <ChildList
          key={`${childList.open.itemId}:${childList.open.type}`}
          open={childList.open}
          projectId={project.id}
          projectKey={project.key}
          boardId={boardId}
          level={level}
          onClose={childList.close}
        />
      ) : null}
    </div>
  );
}
