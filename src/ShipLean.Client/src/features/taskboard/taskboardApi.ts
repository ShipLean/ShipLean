// The Taskboard's write shapes. C#: ShipLean.Contracts.Taskboard. The read model (TaskboardViewDto) is in api/views.ts.
import { useEffect } from 'react';
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

export const taskboardMovesPath = (sprintId: string): string => `${taskboardPath(sprintId)}/moves`;

export const taskboardKey = (projectId: string, sprintId: string) =>
  ['projects', projectId, 'taskboard', sprintId] as const;

/** The key of the Taskboard's move mutations: while one is in flight the board is not refetched (the last move to settle does that). */
export const taskboardMoveKey = (projectId: string, sprintId: string) =>
  ['projects', projectId, 'taskboard-move', sprintId] as const;

// The events that change what the Taskboard shows. Own writes invalidate directly (a tab ignores its own events).
const TASKBOARD_EVENTS = [
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
  useEffect(() => {
    const refetch = () => {
      // A refetch now would show the board without the move still on its way: the last move to settle refetches.
      if (queryClient.isMutating({ mutationKey: taskboardMoveKey(projectId, sprintId) }) > 0)
        return;
      void queryClient.invalidateQueries({ queryKey: taskboardKey(projectId, sprintId) });
    };
    const unregister = TASKBOARD_EVENTS.map((type) => registerInvalidation(type, refetch));
    return () => unregister.forEach((off) => off());
  }, [projectId, sprintId, queryClient]);
  return useQuery({
    queryKey: taskboardKey(projectId, sprintId),
    queryFn: () => api.get<TaskboardViewDto>(taskboardPath(sprintId)),
  });
}

/** Creates a work item in this sprint's Taskboard: a task under a story or bug (or none), or a story or bug in the sprint. */
export function useCreateOnTaskboard(projectId: string, sprintId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (create: { type: WorkItemTypeId; title: string; parentId: string | null }) =>
      api.post<WriteResult>('/api/work-items', {
        projectId,
        type: create.type,
        title: create.title,
        parentId: create.parentId,
        // A task under a story belongs to the sprint through its parent; the others are put in it.
        sprintId: create.parentId === null ? sprintId : null,
        changes: null,
      } satisfies WorkItemCreate),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: taskboardKey(projectId, sprintId) }),
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
export function useSetParentState(projectId: string, sprintId: string) {
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
    onSettled: () => queryClient.invalidateQueries({ queryKey: taskboardKey(projectId, sprintId) }),
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
