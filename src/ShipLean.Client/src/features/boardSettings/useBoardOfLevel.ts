import { useQuery } from '@tanstack/react-query';
import type { BoardLevel } from '../../api/boards';
import { fetchBoards, projectBoardsKey } from '../../queries/shared';

/** The id of the project's board of one level, from the shared boards list. */
export function useBoardOfLevel(projectId: string, level: BoardLevel) {
  const boards = useQuery({
    queryKey: projectBoardsKey(projectId),
    queryFn: () => fetchBoards(projectId),
  });
  const boardId = boards.data?.find((candidate) => candidate.level === level)?.id;
  // The list loaded without a board of this level: nothing to wait for.
  const failed = boardId === undefined && (boards.isError || boards.data !== undefined);
  return { boardId, failed };
}
