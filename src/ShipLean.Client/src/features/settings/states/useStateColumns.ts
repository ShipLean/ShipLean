import { useQuery } from '@tanstack/react-query';
import type { BoardLevel } from '../../../api/boards';
import type { WorkItemTypeId } from '../../../api/refs';
import { fetchBoards, projectBoardsKey } from '../../../queries/shared';
import { boardSettingsKey, fetchBoardSettings } from '../../boardSettings/boardSettingsApi';

const LEVEL_OF: Record<Exclude<WorkItemTypeId, 'task'>, BoardLevel> = {
  epic: 'epics',
  feature: 'features',
  story: 'stories',
  bug: 'stories',
};

/**
 * The board column each saved state of a type sits in (state id to column name), or undefined while the board loads.
 * Tasks sit in a Taskboard column named after the state, so they read nothing.
 */
export function useStateColumns(projectId: string, type: WorkItemTypeId) {
  const level = type === 'task' ? undefined : LEVEL_OF[type];
  const boards = useQuery({
    queryKey: projectBoardsKey(projectId),
    queryFn: () => fetchBoards(projectId),
    enabled: level !== undefined,
  });
  const boardId = boards.data?.find((b) => b.level === level)?.id;
  const board = useQuery({
    queryKey: boardSettingsKey(projectId, boardId ?? ''),
    queryFn: () => fetchBoardSettings(boardId ?? ''),
    enabled: boardId !== undefined,
  });
  if (board.data === undefined) return undefined;
  const columns = new Map<string, string>();
  for (const column of board.data.settings.columns) {
    for (const mapping of column.stateMappings) {
      if (mapping.type === type) columns.set(mapping.stateId, column.name);
    }
  }
  return columns;
}
