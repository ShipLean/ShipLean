// Backlog reads and writes for the phone screens. The shapes are in views.ts and backlogWrites.ts; the routes are the SPA's
// (features/backlog/backlogApi.ts, drag/useReorder.ts).
import { api } from './client';
import type {
  BacklogAddItem,
  BacklogAddRequest,
  BacklogAddResult,
  BulkPlacement,
  BulkWriteItem,
  BulkWriteResult,
} from './backlogWrites';
import type { BacklogViewDto } from './views';
import type { ReorderRequest, WriteResult } from './workItems';

export type BacklogLevel = 'epics' | 'features' | 'stories';

export interface BacklogFilter {
  mode: 'list' | 'tree';
  /** CriteriaJson text; '' for none. */
  filter: string;
  includeCompleted: boolean;
}

export const backlogKeys = {
  /** Every level and mode of the project's backlog. */
  root: (projectId: string) => ['projects', projectId, 'backlog'] as const,
  level: (projectId: string, level: BacklogLevel, filter: BacklogFilter) =>
    [
      ...backlogKeys.root(projectId),
      level,
      filter.mode,
      filter.filter,
      filter.includeCompleted,
    ] as const,
};

export const getBacklog = (projectId: string, level: BacklogLevel, filter: BacklogFilter) => {
  const project = encodeURIComponent(projectId);
  const rest = `filter=${encodeURIComponent(filter.filter)}&includeCompleted=${filter.includeCompleted}`;
  return api.get<BacklogViewDto>(
    level === 'stories'
      ? `/api/projects/${project}/backlog?mode=${filter.mode}&${rest}`
      : `/api/projects/${project}/backlogs/${level}?${filter.mode === 'list' ? '' : `mode=${filter.mode}&`}${rest}`,
  );
};

export const reorder = (itemId: string, expectedRevision: number, beforeItemId: string | null) =>
  api.post<WriteResult>(`/api/work-items/${encodeURIComponent(itemId)}/reorder`, {
    expectedRevision,
    beforeItemId,
  } satisfies ReorderRequest);

export const bulkPlace = (projectId: string, items: BulkWriteItem[]) =>
  api.post<BulkWriteResult>(`/api/projects/${encodeURIComponent(projectId)}/backlog/bulk`, {
    items,
  });

/** A null placement is sent as null, which the server reads as first. */
export const addItems = (
  projectId: string,
  items: BacklogAddItem[],
  placement: BulkPlacement | null,
  sprintId: string | null = null,
) =>
  api.post<BacklogAddResult>(`/api/projects/${encodeURIComponent(projectId)}/backlog/items`, {
    items,
    sprintId,
    placement,
  } satisfies BacklogAddRequest);
