import { useEffect, useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { BoardViewDto } from '../../api/views';
import type { WorkItemCreate, WriteResult } from '../../api/workItems';
import { registerInvalidation } from '../../realtime';
import type { Level } from '../../routes';
import { movesPending } from './moves/pendingMoves';

// A board's react-query keys and reads. Everything is keyed under ['projects', projectId, 'board', boardId, query],
// so one invalidation of boardRootKey reaches every filter and every expanded cell.
export const boardRootKey = (projectId: string, boardId: string) =>
  ['projects', projectId, 'board', boardId] as const;

/**
 * The server-side filters of a board view. The filter bar fills these; `older` is the OlderCursor of each Done cell
 * the user expanded. Absent = no filter. assignee/owner take a user id or `none`, any of several.
 */
export interface BoardFilterQuery {
  q?: string;
  assignee?: string[];
  owner?: string[];
  sprint?: string;
  epic?: string;
}

export interface BoardQuery extends BoardFilterQuery {
  older?: string[];
}

/** A card dropped on a board (sent to POST /api/boards/{id}/moves). laneId null = keep the lane. */
export interface BoardMoveRequest {
  itemId: string;
  expectedRevision: number;
  columnId: string;
  done: boolean;
  laneId?: string | null;
  beforeItemId?: string | null;
}

/** The query string of a board read; the same query always gives the same string, so it is also the cache key. */
export function boardSearch(query: BoardQuery): string {
  const params = new URLSearchParams();
  if (query.q) params.set('q', query.q);
  for (const id of query.assignee ?? []) params.append('assignee', id);
  for (const id of query.owner ?? []) params.append('owner', id);
  if (query.sprint) params.set('sprint', query.sprint);
  if (query.epic) params.set('epic', query.epic);
  for (const cursor of [...(query.older ?? [])].sort()) params.append('older', cursor);
  return params.toString();
}

export const boardKey = (projectId: string, boardId: string, query: BoardQuery) =>
  [...boardRootKey(projectId, boardId), boardSearch(query)] as const;

export const fetchBoard = (boardId: string, query: BoardQuery) => {
  const search = boardSearch(query);
  return api.get<BoardViewDto>(
    `/api/boards/${encodeURIComponent(boardId)}${search === '' ? '' : `?${search}`}`,
  );
};

// What changes a board: the cards, their states, the people on them (members renamed), and the board's own settings.
const BOARD_EVENTS = [
  'projectChanged',
  'workItemCreated',
  'workItemUpdated',
  'workItemMoved',
  'workItemRemoved',
  'statesChanged',
] as const;

/**
 * The board read model, refetched on the project's work item and state events and on this board's settings events.
 * `settingsChanges` counts the settings changes other people made since the board opened (the header flashes on each).
 */
export function useBoard(projectId: string, boardId: string, query: BoardQuery) {
  const queryClient = useQueryClient();
  const [settingsChanges, setSettingsChanges] = useState(0);
  useEffect(() => {
    const refetch = () => {
      // A move in flight shows the card where it was dropped; the last move to settle refetches.
      if (movesPending(boardId)) return;
      void queryClient.invalidateQueries({ queryKey: boardRootKey(projectId, boardId) });
    };
    const unregister: (() => void)[] = BOARD_EVENTS.map((type) =>
      registerInvalidation(type, refetch),
    );
    unregister.push(
      registerInvalidation('boardSettingsChanged', (event) => {
        if (event.boardId !== boardId) return;
        setSettingsChanges((count) => count + 1);
        refetch();
      }),
    );
    return () => unregister.forEach((off) => off());
  }, [projectId, boardId, queryClient]);
  const view = useQuery({
    queryKey: boardKey(projectId, boardId, query),
    queryFn: () => fetchBoard(boardId, query),
    // Expanding a cell or typing in the filter keeps the board on screen until the new answer is in.
    placeholderData: keepPreviousData,
    // A move in flight shows the card where it was dropped: coming back to the tab or the network must not refetch
    // over it. The last move to settle refetches.
    refetchOnWindowFocus: () => !movesPending(boardId),
    refetchOnReconnect: () => !movesPending(boardId),
  });
  return { view, settingsChanges };
}

/** The type a "+ New" card in the first column creates on each level. */
export const NEW_CARD_TYPE: Record<Level, WorkItemCreate['type']> = {
  stories: 'story',
  features: 'feature',
  epics: 'epic',
};

/** Creates a work item of the level's type; the store puts it in the first column. */
export function useCreateCard(projectId: string, boardId: string, level: Level) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (title: string) =>
      api.post<WriteResult>('/api/work-items', {
        projectId,
        type: NEW_CARD_TYPE[level],
        title,
        parentId: null,
        sprintId: null,
        changes: null,
      } satisfies WorkItemCreate),
    // The last move to settle refetches, so a new card does not refetch over moves on their way.
    onSuccess: () =>
      movesPending(boardId)
        ? undefined
        : queryClient.invalidateQueries({ queryKey: boardRootKey(projectId, boardId) }),
  });
}
