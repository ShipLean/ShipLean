import { useEffect } from 'react';
import { useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { AttachmentDto } from '../../api/attachments';
import type { CommentCreate, CommentDto, CommentEdit } from '../../api/comments';
import type { StateDto } from '../../api/projects';
import type { WorkItemTypeId } from '../../api/refs';
import type {
  HistoryDto,
  WorkItemCreate,
  WorkItemDto,
  WorkItemPatch,
  WriteResult,
} from '../../api/workItems';
import { registerInvalidation } from '../../realtime';
import {
  fetchMembers,
  fetchSprints,
  projectMembersKey,
  projectSprintsKey,
} from '../../queries/shared';

// The work item's react-query keys, all under the project like the rest of its data. Lane F slices add their hooks here.
export const workItemKey = (projectId: string, itemKey: string) =>
  ['projects', projectId, 'work-items', itemKey] as const;
export const projectStatesKey = (projectId: string) => ['projects', projectId, 'states'] as const;
export const workItemListKey = (projectId: string, type: WorkItemTypeId) =>
  ['projects', projectId, 'work-items', 'list', type] as const;
export const workItemHistoryKey = (projectId: string, itemKey: string) =>
  [...workItemKey(projectId, itemKey), 'history'] as const;
export const workItemCommentsKey = (projectId: string, itemKey: string) =>
  [...workItemKey(projectId, itemKey), 'comments'] as const;
export const workItemAttachmentsKey = (projectId: string, itemKey: string) =>
  [...workItemKey(projectId, itemKey), 'attachments'] as const;
export const renderedMarkdownKey = (markdown: string) => ['markdown', 'render', markdown] as const;

const projectPath = (projectId: string) => `/api/projects/${encodeURIComponent(projectId)}`;

export const fetchWorkItem = (projectId: string, itemKey: string) =>
  api.get<WorkItemDto>(
    `${projectPath(projectId)}/work-items/by-key/${encodeURIComponent(itemKey)}`,
  );

export const patchWorkItem = (id: string, patch: WorkItemPatch) =>
  api.patch<WriteResult>(`/api/work-items/${encodeURIComponent(id)}`, patch);

export const createWorkItem = (create: WorkItemCreate) =>
  api.post<WriteResult>('/api/work-items', create);

export const fetchComments = (workItemId: string) =>
  api.get<CommentDto[]>(`/api/work-items/${encodeURIComponent(workItemId)}/comments`);

export const addComment = (workItemId: string, create: CommentCreate) =>
  api.post<CommentDto>(`/api/work-items/${encodeURIComponent(workItemId)}/comments`, create);

export const editComment = (id: string, edit: CommentEdit) =>
  api.patch<CommentDto>(`/api/comments/${encodeURIComponent(id)}`, edit);

export const supersedeComment = (id: string) =>
  api.post<CommentDto>(`/api/comments/${encodeURIComponent(id)}/supersede`);

export const archiveComment = (id: string) =>
  api.post<void>(`/api/comments/${encodeURIComponent(id)}/archive`);

export const fetchAttachments = (workItemId: string) =>
  api.get<AttachmentDto[]>(`/api/work-items/${encodeURIComponent(workItemId)}/attachments`);

export const archiveAttachment = (id: string) =>
  api.post<void>(`/api/attachments/${encodeURIComponent(id)}/archive`);

export const fetchHistory = (workItemId: string) =>
  api.get<HistoryDto>(`/api/work-items/${encodeURIComponent(workItemId)}/history`);

export const renderMarkdown = (markdown: string) =>
  api.post<{ markdown: string; html: string }>('/api/markdown/render', { markdown });

/** The item, refetched when another tab or person changes it (its `workItemUpdated`, moved or removed event). */
export function useWorkItem(projectId: string, itemKey: string) {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: workItemKey(projectId, itemKey),
    queryFn: () => fetchWorkItem(projectId, itemKey),
  });
  const id = query.data?.id;
  useEffect(() => {
    if (id === undefined) return undefined;
    const refetch = (event: { id: string }) => {
      if (event.id === id) {
        void queryClient.invalidateQueries({ queryKey: workItemKey(projectId, itemKey) });
      }
    };
    const unregister = [
      registerInvalidation('workItemUpdated', refetch),
      registerInvalidation('workItemMoved', refetch),
      registerInvalidation('workItemRemoved', refetch),
    ];
    return () => unregister.forEach((off) => off());
  }, [id, projectId, itemKey, queryClient]);
  return query;
}

// Refetches `key` when a remote event of `type` arrives, for data the form reads that other people change.
function useRefetchOn(type: 'statesChanged' | 'sprintChanged' | 'projectChanged', key: QueryKey) {
  const queryClient = useQueryClient();
  const serialised = JSON.stringify(key);
  useEffect(
    () =>
      registerInvalidation(type, () => {
        void queryClient.invalidateQueries({ queryKey: JSON.parse(serialised) as QueryKey });
      }),
    [type, serialised, queryClient],
  );
}

export function useProjectStates(projectId: string) {
  useRefetchOn('statesChanged', projectStatesKey(projectId));
  return useQuery({
    queryKey: projectStatesKey(projectId),
    queryFn: () => api.get<StateDto[]>(`${projectPath(projectId)}/states`),
  });
}

export function useProjectMembers(projectId: string) {
  useRefetchOn('projectChanged', projectMembersKey(projectId));
  return useQuery({
    queryKey: projectMembersKey(projectId),
    queryFn: () => fetchMembers(projectId),
  });
}

export function useProjectSprints(projectId: string) {
  useRefetchOn('sprintChanged', projectSprintsKey(projectId));
  return useQuery({
    queryKey: projectSprintsKey(projectId),
    queryFn: () => fetchSprints(projectId),
  });
}

/** Work items of one type, for the parent picker; fetched only while the picker is open. */
export const useWorkItemsOfType = (projectId: string, type: WorkItemTypeId, enabled: boolean) =>
  useQuery({
    queryKey: workItemListKey(projectId, type),
    queryFn: () => api.get<WorkItemDto[]>(`${projectPath(projectId)}/work-items?type=${type}`),
    enabled,
  });

/** The server's HTML for markdown that differs from what the item already carries rendered. */
export const useRenderedMarkdown = (markdown: string, enabled: boolean) =>
  useQuery({
    queryKey: renderedMarkdownKey(markdown),
    queryFn: () => renderMarkdown(markdown),
    enabled,
    staleTime: Infinity,
  });
