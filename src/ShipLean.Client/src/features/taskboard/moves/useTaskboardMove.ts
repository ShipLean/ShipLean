import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useRef } from 'react';
import { api } from '../../../api/client';
import type { TaskboardViewDto } from '../../../api/views';
import { conflictOf } from '../../../api/workItems';
import { showToast } from '../../../components/toastStore';
import {
  taskboardKey,
  taskboardMoveKey,
  taskboardMovesPath,
  type TaskboardGrouping,
  type TaskboardMoveRequest,
  type TaskboardMoveResultDto,
} from '../taskboardApi';
import { applyMove, offerResolveOn, revisionOf, withRevision, type AppliedMove } from './applyMove';

interface Sent {
  request: TaskboardMoveRequest;
  applied: AppliedMove;
  /** Puts the card back where it was if the server refuses the move. */
  undo: AppliedMove;
  key: string;
}

/**
 * Sends a move and shows it at once. Moves go one at a time, each naming the revision the board holds for its card
 * when it is sent (the previous move of that card has written the new one). A move the server refuses puts that card
 * back, says so in a toast and leaves other moves on their way alone. The board is refetched when the last move settles.
 */
export function useTaskboardMove(projectId: string, sprintId: string, grouping: TaskboardGrouping) {
  const queryClient = useQueryClient();
  const queryKey = taskboardKey(projectId, sprintId);
  const inFlight = useRef(0);
  const mutation = useMutation({
    mutationKey: taskboardMoveKey(projectId, sprintId),
    scope: { id: `taskboard-move:${sprintId}` },
    mutationFn: ({ request }: Sent) => {
      const held = revisionOf(queryClient.getQueryData<TaskboardViewDto>(queryKey), request.taskId);
      return api.post<TaskboardMoveResultDto>(taskboardMovesPath(sprintId), {
        ...request,
        expectedRevision: held ?? request.expectedRevision,
      });
    },
    onMutate: async ({ applied }) => {
      inFlight.current += 1;
      await queryClient.cancelQueries({ queryKey });
      queryClient.setQueryData<TaskboardViewDto>(queryKey, (view) =>
        view ? applyMove(view, grouping, applied) : view,
      );
    },
    onSuccess: (result, { request, key, applied }) => {
      const { parentId, offerResolve, written } = result;
      queryClient.setQueryData<TaskboardViewDto>(queryKey, (view) => {
        if (!view) return view;
        const updated = withRevision(view, request.taskId, written.revision);
        return offerResolve && parentId ? offerResolveOn(updated, parentId) : updated;
      });
      const column = queryClient
        .getQueryData<TaskboardViewDto>(queryKey)
        ?.columns.find((c) => c.state.id === applied.stateId);
      showToast(column ? `${key} moved to ${column.state.name}.` : `${key} moved.`, {
        tone: 'ok',
      });
    },
    onError: (error, { key, undo }) => {
      queryClient.setQueryData<TaskboardViewDto>(queryKey, (view) =>
        view ? applyMove(view, grouping, undo) : view,
      );
      showToast(
        conflictOf(error)
          ? `${key} was changed by someone else, so it went back.`
          : `${key} could not be moved.`,
        { tone: 'warn' },
      );
    },
    onSettled: () => {
      inFlight.current -= 1;
      if (inFlight.current === 0) void queryClient.invalidateQueries({ queryKey });
    },
  });
  return mutation.mutate;
}
