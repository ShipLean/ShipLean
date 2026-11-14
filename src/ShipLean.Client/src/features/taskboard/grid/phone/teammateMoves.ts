import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { taskboardMoveKey } from '../../taskboardApi';
import type { TaskboardGridView } from '../../taskboardGridView';
import type { TaskboardScope } from '../../taskboardScope';

/** Which state each task is in, over the People rows (they hold every task once). */
function stateOfTasks(view: TaskboardGridView): Map<string, string> {
  const states = new Map<string, string>();
  for (const row of view.peopleRows)
    for (const cell of row.cells) for (const task of cell.tasks) states.set(task.id, cell.stateId);
  return states;
}

/** The states a task landed in between two views: tasks that were in another state before. New tasks and removed ones are not moves. */
export function landedStates(before: TaskboardGridView, after: TaskboardGridView): string[] {
  const was = stateOfTasks(before);
  const landed = new Set<string>();
  stateOfTasks(after).forEach((stateId, taskId) => {
    const from = was.get(taskId);
    if (from !== undefined && from !== stateId) landed.add(stateId);
  });
  return [...landed];
}

/**
 * A counter per state that goes up each time a teammate's move lands a task there, for the pager's chip pulse. A view that
 * changes while one of this tab's own moves is pending is its own move (shown at once, then settled), so it is not told.
 */
export function useTeammatePulses(
  projectId: string,
  scope: TaskboardScope,
  view: TaskboardGridView,
): ReadonlyMap<string, number> {
  const queryClient = useQueryClient();
  const previous = useRef(view);
  const [pulses, setPulses] = useState<ReadonlyMap<string, number>>(new Map());
  useEffect(() => {
    const before = previous.current;
    previous.current = view;
    if (before === view) return;
    if (queryClient.isMutating({ mutationKey: taskboardMoveKey(projectId, scope) }) > 0) return;
    const landed = landedStates(before, view);
    if (landed.length === 0) return;
    setPulses((now) => {
      const next = new Map(now);
      landed.forEach((stateId) => next.set(stateId, (now.get(stateId) ?? 0) + 1));
      return next;
    });
  }, [view, projectId, scope, queryClient]);
  return pulses;
}
