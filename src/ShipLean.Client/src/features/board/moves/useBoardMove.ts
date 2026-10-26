import { useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { api, ApiRequestError } from '../../../api/client';
import type { BoardViewDto } from '../../../api/views';
import { conflictOf, type WriteResult } from '../../../api/workItems';
import { showToast } from '../../../components/toastStore';
import type { Level } from '../../../routes';
import { boardRootKey, type BoardMoveRequest } from '../boardApi';
import { applyMove, locate, withRevision, type DropTarget } from './applyMove';
import { moveSettled, moveStarted } from './pendingMoves';
import type { MoveBody } from './moveRequests';

export interface Sent {
  body: MoveBody;
  to: DropTarget;
  key: string;
  /** The column's name: the toast names it when the move changed no state. */
  columnName: string;
  /** What a move into Done leaves open below the card ("2 stories"); the toast says so. */
  stillOpen?: string;
}

const PULLED = 'pulled';

// The revision the cards of the board hold now: a move waiting behind another reads it after that one landed.
function revisionOf(queryClient: QueryClient, root: readonly unknown[], itemId: string) {
  for (const [, view] of queryClient.getQueriesData<BoardViewDto>({ queryKey: root })) {
    const found = view && locate(view, itemId);
    if (found) return found.card.revision;
  }
  return undefined;
}

interface Starting {
  queryKey: readonly unknown[];
  /** Where the card stood in that view: dropped above the card that followed it. */
  place: DropTarget;
  /** The state the card had. */
  stateName: string;
}

function startingPlaces(queryClient: QueryClient, root: readonly unknown[], itemId: string) {
  const places: Starting[] = [];
  for (const [queryKey, view] of queryClient.getQueriesData<BoardViewDto>({ queryKey: root })) {
    const at = view && locate(view, itemId);
    if (at) {
      places.push({
        queryKey,
        stateName: at.card.state.name,
        place: {
          laneId: at.laneId,
          columnId: at.columnId,
          done: at.done,
          beforeItemId: at.cards[at.index + 1]?.id ?? null,
        },
      });
    }
  }
  return places;
}

/**
 * Sends moves one at a time (a scope per board) and shows each at once. A move the server refuses puts that card
 * back where it stood (the other moves on their way stay), refetches, and says why in a toast; a stale revision says someone else changed the card.
 */
export function useBoardMove(projectId: string, boardId: string, level: Level) {
  const queryClient = useQueryClient();
  const root = boardRootKey(projectId, boardId);
  const mutation = useMutation({
    mutationKey: ['board-move', boardId],
    scope: { id: `board-moves:${boardId}` },
    mutationFn: ({ body }: Sent) => {
      const expectedRevision = revisionOf(queryClient, root, body.itemId);
      if (expectedRevision === undefined) throw new Error('The card is no longer on the board.');
      const request: BoardMoveRequest = { ...body, expectedRevision };
      return api.post<WriteResult>(`/api/boards/${encodeURIComponent(boardId)}/moves`, request);
    },
    onMutate: async ({ body, to }) => {
      moveStarted(boardId);
      await queryClient.cancelQueries({ queryKey: root });
      // Where the card stood in each view of the board, so a refused move puts back this card and nothing else.
      const back = startingPlaces(queryClient, root, body.itemId);
      queryClient.setQueriesData<BoardViewDto>({ queryKey: root }, (view) =>
        view ? applyMove(view, body.itemId, to) : view,
      );
      return { back };
    },
    onSuccess: (result, { body, key, columnName, stillOpen }, context) => {
      queryClient.setQueriesData<BoardViewDto>({ queryKey: root }, (view) =>
        view ? withRevision(view, body.itemId, result.revision) : view,
      );
      const state = result.changes.find((change) => change.field === 'state')?.toLabel;
      const stateText = state
        ? `state ${state}`
        : `state stays ${context?.back[0]?.stateName ?? ''}`;
      showToast(
        `${key} → ${columnName} (${stateText})${stillOpen ? ` · ${stillOpen} still open` : ''}`,
        { tone: 'ok' },
      );
      if (level === 'stories' && result.notices.includes(PULLED)) {
        showToast('Assigned to you', { tone: 'ok' });
      }
    },
    onError: (error, { body, key }, context) => {
      for (const { queryKey, place } of context?.back ?? [])
        queryClient.setQueryData<BoardViewDto>(queryKey, (view) =>
          view ? applyMove(view, body.itemId, place) : view,
        );
      showToast(
        conflictOf(error)
          ? `${key} was changed by someone else, so it went back.`
          : `${key} could not be moved${error instanceof ApiRequestError ? `: ${error.message}` : '.'}`,
        { tone: 'warn' },
      );
    },
    onSettled: () => {
      if (moveSettled(boardId)) void queryClient.invalidateQueries({ queryKey: root });
    },
  });
  return mutation.mutate;
}
