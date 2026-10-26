import { useEffect } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { WorkItemCreate, WriteResult } from '../../api/workItems';
import type { BacklogViewDto } from '../../api/views';
import { registerInvalidation } from '../../realtime';
import { fetchPreference, preferenceKey } from '../../queries/shared';
import type { BacklogMode, Level } from '../../routes';

// The backlog's react-query keys and reads, shared by every backlog mode and level. Everything is keyed under
// ['projects', projectId, 'backlog', …] so one invalidation reaches every mode.
export const backlogRootKey = (projectId: string) => ['projects', projectId, 'backlog'] as const;
export const backlogKey = (
  projectId: string,
  level: Level,
  mode: BacklogMode,
  filter: string,
  includeCompleted: boolean,
) => [...backlogRootKey(projectId), level, mode, filter, includeCompleted] as const;

export interface BacklogQuery {
  projectId: string;
  level: Level;
  // Stories only: Features and Epics have no modes.
  mode: BacklogMode;
  // The URL-encoded-by-us CriteriaJson text of the filter; '' for none.
  filter: string;
  includeCompleted: boolean;
}

export const fetchBacklog = ({
  projectId,
  level,
  mode,
  filter,
  includeCompleted,
}: BacklogQuery) => {
  const project = encodeURIComponent(projectId);
  const rest = `filter=${encodeURIComponent(filter)}&includeCompleted=${includeCompleted}`;
  return api.get<BacklogViewDto>(
    level === 'stories'
      ? `/api/projects/${project}/backlog?mode=${mode}&${rest}`
      : `/api/projects/${project}/backlogs/${level}?${rest}`,
  );
};

// The events that change what a backlog shows: the items, their order, sprints (chips), states, the board's tag
// colours and the project's forecast and velocity. Each refetches the backlog the screen holds; own writes invalidate directly (a tab ignores its own events).
const BACKLOG_EVENTS = [
  'workItemCreated',
  'workItemUpdated',
  'workItemMoved',
  'workItemReordered',
  'workItemRemoved',
  'boardSettingsChanged',
  'sprintChanged',
  'statesChanged',
  'projectChanged',
] as const;

/** The backlog read model, refetched on the project's work item, sprint, state and board settings events. */
export function useBacklog(query: BacklogQuery) {
  const queryClient = useQueryClient();
  const { projectId } = query;
  useEffect(() => {
    const refetch = () => {
      void queryClient.invalidateQueries({ queryKey: backlogRootKey(projectId) });
    };
    const unregister = BACKLOG_EVENTS.map((type) => registerInvalidation(type, refetch));
    return () => unregister.forEach((off) => off());
  }, [projectId, queryClient]);
  return useQuery({
    queryKey: backlogKey(projectId, query.level, query.mode, query.filter, query.includeCompleted),
    queryFn: () => fetchBacklog(query),
    // A new filter keeps the rows on screen until its answer arrives.
    placeholderData: keepPreviousData,
  });
}

/** Creates a work item; the store puts it first in the backlog. */
export function useCreateWorkItem(projectId: string, projectKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (create: Pick<WorkItemCreate, 'type' | 'title'>) =>
      api.post<WriteResult>('/api/work-items', {
        projectId,
        type: create.type,
        title: create.title,
        parentId: null,
        sprintId: null,
        changes: null,
      } satisfies WorkItemCreate),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: backlogRootKey(projectId) }),
        queryClient.invalidateQueries({ queryKey: ['projects', 'by-key', projectKey] }),
      ]);
    },
  });
}

// The columns the Stories list can show, in order. Key, Title and # are always shown.
export const BACKLOG_COLUMNS = [
  { id: 'rank', label: '#', required: true },
  { id: 'key', label: 'Key', required: true },
  { id: 'title', label: 'Title', required: true },
  { id: 'parent', label: 'Parent', required: false },
  { id: 'state', label: 'State', required: false },
  { id: 'points', label: 'Points', required: false },
  { id: 'sprint', label: 'Sprint', required: false },
  { id: 'assignee', label: 'Assignee', required: false },
  { id: 'tags', label: 'Tags', required: false },
] as const;
export type BacklogColumnId = (typeof BACKLOG_COLUMNS)[number]['id'];

// The saved preference `view:backlog`: the columns each level hides (nothing saved = every column shows).
export interface BacklogViewPreference {
  hiddenColumns?: Partial<Record<Level, string[]>>;
  // Planning mode's Forecast toggle; absent = on.
  forecast?: boolean;
}

const VIEW_KEY = 'view:backlog';
export const backlogViewKey = preferenceKey(VIEW_KEY);

export interface ColumnSpec {
  id: string;
  label: string;
  required: boolean;
}

/** The ids of the optional columns the preference hides for a level; required and unknown ids are dropped. */
export function hiddenColumnsOf(
  preference: unknown,
  level: Level,
  columns: readonly ColumnSpec[],
): string[] {
  const saved = (preference as BacklogViewPreference | null)?.hiddenColumns?.[level];
  return columns.filter((c) => !c.required && saved?.includes(c.id)).map((c) => c.id);
}

/**
 * The signed-in user's `view:backlog` preference for one level (whose columns are given), a saver for that level's
 * hidden columns and `update(patch)` for any other field. Every writer spreads the cached value, so none clobbers another.
 */
export function useBacklogView(level: Level, columns: readonly ColumnSpec[]) {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: backlogViewKey,
    queryFn: () => fetchPreference(VIEW_KEY, {}),
  });
  const save = useMutation({
    mutationFn: (next: BacklogViewPreference) =>
      api.put<void>(`/api/me/preferences/${encodeURIComponent(VIEW_KEY)}`, next),
    onError: () => queryClient.invalidateQueries({ queryKey: backlogViewKey }),
  });
  // The change shows at once; a failed save puts the saved value back.
  function update(patch: Partial<BacklogViewPreference>) {
    const current = (queryClient.getQueryData(backlogViewKey) ?? {}) as BacklogViewPreference;
    const next: BacklogViewPreference = { ...current, ...patch };
    queryClient.setQueryData(backlogViewKey, next);
    save.mutate(next);
  }
  function setHidden(hidden: string[]) {
    const current = (queryClient.getQueryData(backlogViewKey) ?? {}) as BacklogViewPreference;
    update({
      hiddenColumns: {
        ...current.hiddenColumns,
        [level]: hiddenColumnsOf({ hiddenColumns: { [level]: hidden } }, level, columns),
      },
    });
  }
  return {
    loaded: query.isSuccess,
    failed: query.isError,
    hidden: query.isSuccess ? hiddenColumnsOf(query.data, level, columns) : [],
    // Planning mode's Forecast toggle: on unless the preference says otherwise.
    forecast: !query.isSuccess || (query.data as BacklogViewPreference | null)?.forecast !== false,
    setHidden,
    update,
  };
}
