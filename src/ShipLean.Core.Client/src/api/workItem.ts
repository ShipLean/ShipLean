// Work item fetchers for the phone screens: the read by key, the revision-checked write and the children. The shapes are in
// workItems.ts. Routes are the SPA's (features/workItem/workItemApi.ts).
import { api, type RequestOptions } from './client';
import type { ChildSummaryDto, WorkItemChanges, WorkItemDto, WriteResult } from './workItems';

export const workItemKeys = {
  byKey: (projectId: string, itemKey: string) => ['workItem', 'byKey', projectId, itemKey] as const,
  children: (itemId: string) => ['workItem', 'children', itemId] as const,
  /** Every item of the project: invalidates the by-key reads and the children lists. */
  item: (projectId: string) => ['workItem', 'byKey', projectId] as const,
};

export const getItemByKey = (projectId: string, itemKey: string, options?: RequestOptions) =>
  api.get<WorkItemDto>(
    `/api/projects/${encodeURIComponent(projectId)}/work-items/by-key/${encodeURIComponent(itemKey)}`,
    options,
  );

export const patchItem = (id: string, expectedRevision: number, changes: WorkItemChanges) =>
  api.patch<WriteResult>(`/api/work-items/${encodeURIComponent(id)}`, {
    expectedRevision,
    changes,
  });

export const getChildren = (itemId: string, type?: string) =>
  api.get<ChildSummaryDto[]>(
    `/api/work-items/${encodeURIComponent(itemId)}/children${
      type === undefined ? '' : `?type=${encodeURIComponent(type)}`
    }`,
  );
