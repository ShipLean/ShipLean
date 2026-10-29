// The Taskboard's write shapes. C#: ShipLean.Contracts.Taskboard. The read model (TaskboardViewDto) is in api/views.ts.
import { useEffect, useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { WorkItemTypeId } from '../../api/refs';
import type { TaskboardViewDto } from '../../api/views';
import {
  conflictOf,
  type WorkItemChanges,
  type WorkItemCreate,
  type WriteResult,
} from '../../api/workItems';
import { showToast } from '../../components/toastStore';
import { registerInvalidation } from '../../realtime';
import { fetchPreference, preferenceKey } from '../../queries/shared';
import { scopeId, type TaskboardScope } from './taskboardScope';

export type TaskboardGrouping = 'stories' | 'people';

/** target is the row's story or bug (stories; null = the Unparented row) or its person (people; null = Unassigned). */
export interface TaskboardMoveRow {
  grouping: TaskboardGrouping;
  target: string | null;
}

/** stateId is the column (a Task state); row is null when the drop stays in its row; beforeItemId is the task the card was dropped above (null = last in the cell). A People drop (row.grouping 'people') ignores beforeItemId: the task goes last in its (state, parent) cell, and People cells list tasks in parent row order, then cell order. */
export interface TaskboardMoveRequest {
  taskId: string;
  expectedRevision: number;
  stateId: string;
  row: TaskboardMoveRow | null;
  beforeItemId: string | null;
}

/** offerResolve: the move closed the last open task of the parent's row, so offer Move to Resolved. It describes the destination row only; the GET is the source of truth for every row. */
export interface TaskboardMoveResultDto {
  written: WriteResult;
  parentId: string | null;
  offerResolve: boolean;
}

export const taskboardPath = (sprintId: string): string => `/api/sprints/${sprintId}/taskboard`;

export const taskboardMovesPath = (scope: TaskboardScope): string =>
  scope.kind === 'sprint'
    ? `${taskboardPath(scope.sprintId)}/moves`
    : `/api/work-items/${scope.featureId}/feature-board/moves`;

/** The query key of the board's view: the Taskboard's for a sprint, the Feature board's for a Feature. */
export const taskboardKey = (projectId: string, scope: TaskboardScope) =>
  [
    'projects',
    projectId,
    scope.kind === 'sprint' ? 'taskboard' : 'feature-board',
    scopeId(scope),
  ] as const;

/** The key of the Taskboard's move mutations: while one is in flight the board is not refetched (the last move to settle does that). */
export const taskboardMoveKey = (projectId: string, scope: TaskboardScope) =>
  [
    'projects',
    projectId,
    scope.kind === 'sprint' ? 'taskboard-move' : 'feature-board-move',
    scopeId(scope),
  ] as const;

// The events that change what a board (the Taskboard or the Feature board) shows. Own writes invalidate directly (a tab ignores its own events).
export const TASKBOARD_EVENTS = [
  'workItemCreated',
  'workItemUpdated',
  'workItemMoved',
  'workItemReordered',
  'workItemRemoved',
  'boardSettingsChanged',
  'sprintChanged',
  'statesChanged',
] as const;

/** The Taskboard read model, refetched on the project's work item, sprint, state and board settings events. */
export function useTaskboard(projectId: string, sprintId: string) {
  const queryClient = useQueryClient();
  const scope = useMemo<TaskboardScope>(() => ({ kind: 'sprint', sprintId }), [sprintId]);
  useEffect(() => {
    const refetch = () => {
      // A refetch now would show the board without the move still on its way: the last move to settle refetches.
      if (queryClient.isMutating({ mutationKey: taskboardMoveKey(projectId, scope) }) > 0) return;
      void queryClient.invalidateQueries({ queryKey: taskboardKey(projectId, scope) });
    };
    const unregister = TASKBOARD_EVENTS.map((type) => registerInvalidation(type, refetch));
    return () => unregister.forEach((off) => off());
  }, [projectId, scope, queryClient]);
  return useQuery({
    queryKey: taskboardKey(projectId, scope),
    queryFn: () => api.get<TaskboardViewDto>(taskboardPath(sprintId)),
  });
}

/** Creates a work item on a board: a task under its row's story or bug, or a story or bug put in the sprint (sprint scope) or under the Feature (Feature scope). Nothing created on a Feature board is put in a sprint. */
export function useCreateOnTaskboard(projectId: string, scope: TaskboardScope) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (create: { type: WorkItemTypeId; title: string; parentId: string | null }) =>
      api.post<WriteResult>('/api/work-items', {
        projectId,
        type: create.type,
        title: create.title,
        // A story or bug added on a Feature board goes under the Feature.
        parentId: create.parentId ?? (scope.kind === 'feature' ? scope.featureId : null),
        // A task under a story belongs to the sprint through its parent; a story or bug is put in the board's sprint.
        sprintId: create.parentId === null && scope.kind === 'sprint' ? scope.sprintId : null,
        changes: null,
      } satisfies WorkItemCreate),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: taskboardKey(projectId, scope) }),
  });
}

const NO_CHANGES: WorkItemChanges = {
  title: null,
  description: null,
  acceptanceCriteria: null,
  reproSteps: null,
  severity: null,
  state: null,
  assignee: null,
  sprint: null,
  priority: null,
  points: null,
  parent: null,
  tags: null,
  addLinks: [],
  removeLinkIds: [],
};

/** Sets a story's or bug's state from its row header. */
export function useSetParentState(projectId: string, scope: TaskboardScope) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (change: { id: string; key: string; revision: number; stateId: string }) =>
      api.patch<WriteResult>(`/api/work-items/${encodeURIComponent(change.id)}`, {
        expectedRevision: change.revision,
        changes: { ...NO_CHANGES, state: { value: change.stateId } },
      }),
    onError: (error, change) =>
      showToast(
        conflictOf(error)
          ? `${change.key} was changed by someone else, so its state stayed as it was.`
          : `${change.key} could not change state.`,
        { tone: 'warn' },
      ),
    onSettled: () => queryClient.invalidateQueries({ queryKey: taskboardKey(projectId, scope) }),
  });
}

// The saved preference `view:taskboard`: how the rows are grouped (nothing saved = Stories).
export interface TaskboardViewPreference {
  groupBy?: TaskboardGrouping;
}

const VIEW_KEY = 'view:taskboard';
export const taskboardViewKey = preferenceKey(VIEW_KEY);

/** The signed-in user's `view:taskboard` preference and a saver for the grouping. */
export function useTaskboardGrouping() {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: taskboardViewKey,
    queryFn: () => fetchPreference(VIEW_KEY, {}),
  });
  const save = useMutation({
    mutationFn: (next: TaskboardViewPreference) =>
      api.put<void>(`/api/me/preferences/${encodeURIComponent(VIEW_KEY)}`, next),
    onError: () => queryClient.invalidateQueries({ queryKey: taskboardViewKey }),
  });
  const saved = (query.data as TaskboardViewPreference | undefined)?.groupBy;
  // The choice shows at once; a failed save puts the saved value back.
  function setGrouping(groupBy: TaskboardGrouping) {
    const next: TaskboardViewPreference = { ...(query.data as TaskboardViewPreference), groupBy };
    queryClient.setQueryData(taskboardViewKey, next);
    save.mutate(next);
  }
  const grouping: TaskboardGrouping = saved === 'people' ? 'people' : 'stories';
  return { loaded: !query.isPending, grouping, setGrouping };
}
