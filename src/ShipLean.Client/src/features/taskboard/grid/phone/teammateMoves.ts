import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { taskboardKey, taskboardMoveKey, type TaskboardMoveRequest } from '../../taskboardApi';
import type { TaskboardGridView } from '../../taskboardGridView';
import type { TaskboardScope } from '../../taskboardScope';

/** Which state each task is in, over the People rows (they hold every task once). */
function stateOfTasks(view: TaskboardGridView): Map<string, string> {
  const states = new Map<string, string>();
  for (const row of view.peopleRows)
    for (const cell of row.cells) for (const task of cell.tasks) states.set(task.id, cell.stateId);
  return states;
}

/**
 * The states a task landed in between two views: tasks that were in another state before. New tasks and removed ones are not
 * moves, and neither are the tasks in `own` (this tab's own moves, and the rollback of a refused one).
 */
export function landedStates(
  before: TaskboardGridView,
  after: TaskboardGridView,
  own: ReadonlySet<string> = new Set(),
): string[] {
  const was = stateOfTasks(before);
  const landed = new Set<string>();
  stateOfTasks(after).forEach((stateId, taskId) => {
    const from = was.get(taskId);
    if (from !== undefined && from !== stateId && !own.has(taskId)) landed.add(stateId);
  });
  return [...landed];
}

/**
 * A counter per state that goes up each time a teammate's move lands a task there, for the pager's chip pulse. Changes to a
 * task this tab moves are its own: shown at once, rolled back if the server refuses, and never told. The rollback reaches
 * the view after the move has settled, so a move counts as own until the board has been read again after it settled.
 */
export function useTeammatePulses(
  projectId: string,
  scope: TaskboardScope,
  view: TaskboardGridView,
): ReadonlyMap<string, number> {
  const queryClient = useQueryClient();
  const previous = useRef(view);
  // Moves of this tab whose views have been looked at already.
  const consumed = useRef(new WeakSet<object>());
  const [pulses, setPulses] = useState<ReadonlyMap<string, number>>(new Map());
  useEffect(() => {
    const before = previous.current;
    previous.current = view;
    if (before === view) return;
    const mutationKey = taskboardMoveKey(projectId, scope);
    const moves = queryClient
      .getMutationCache()
      .findAll({ mutationKey })
      .filter((mutation) => !consumed.current.has(mutation));
    const own = new Set(
      moves.flatMap((mutation) => {
        const sent = mutation.state.variables as { request?: TaskboardMoveRequest } | undefined;
        return sent?.request ? [sent.request.taskId] : [];
      }),
    );
    const pending = queryClient.isMutating({ mutationKey }) > 0;
    // The refetch that follows a settled move is still to come while the board is being read: its view is the move's too.
    const settled =
      !pending && queryClient.isFetching({ queryKey: taskboardKey(projectId, scope) }) === 0;
    if (settled) moves.forEach((mutation) => consumed.current.add(mutation));
    const landed = landedStates(before, view, own);
    if (pending || landed.length === 0) return;
    setPulses((now) => {
      const next = new Map(now);
      landed.forEach((stateId) => next.set(stateId, (now.get(stateId) ?? 0) + 1));
      return next;
    });
  }, [view, projectId, scope, queryClient]);
  return pulses;
}
