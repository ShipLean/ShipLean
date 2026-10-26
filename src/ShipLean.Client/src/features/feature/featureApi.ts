import { useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type {
  AttachmentCheckDto,
  AttachmentCheckRequest,
  AttachmentMarkdownDto,
} from '../../api/attachments';
import type {
  AddLinkCardRequest,
  AddWorkItemLinkRequest,
  EditLinkCardRequest,
  LinkCardDto,
  LinkCardsDto,
  LinkPreviewDto,
  WorkItemLinkRowDto,
} from '../../api/linkCards';
import type { RenderedMarkdownDto } from '../../api/markdown';
import { registerInvalidation } from '../../realtime';
import { workItemKey } from '../workItem/workItemApi';

// The Feature form's reads and writes beyond the work item itself: link cards (saved straight away, never through the
// form's draft), the upload check, a Markdown attachment's page, and Markdown rendered against an item's attachments.
// Keys sit under the project, so a reconnect refetches them with the rest of its data.

const workItemPath = (workItemId: string) => `/api/work-items/${encodeURIComponent(workItemId)}`;

export const linkCardsKey = (projectId: string, itemKey: string) =>
  [...workItemKey(projectId, itemKey), 'link-cards'] as const;
export const attachmentMarkdownKey = (projectId: string, attachmentId: string) =>
  ['projects', projectId, 'attachments', attachmentId, 'markdown'] as const;
export const itemMarkdownKey = (markdown: string, workItemId: string | undefined) =>
  ['markdown', 'render', workItemId ?? null, markdown] as const;

export const fetchLinkCards = (workItemId: string) =>
  api.get<LinkCardsDto>(`${workItemPath(workItemId)}/link-cards`);

/** What adding `url` would show. Saves nothing; an unreachable host is an answer, not an error. */
export const previewLink = (workItemId: string, url: string) =>
  api.post<LinkPreviewDto>(`${workItemPath(workItemId)}/link-cards/preview`, { url });

export const addLinkCard = (workItemId: string, request: AddLinkCardRequest) =>
  api.post<LinkCardDto>(`${workItemPath(workItemId)}/link-cards`, request);

export const addWorkItemLink = (workItemId: string, request: AddWorkItemLinkRequest) =>
  api.post<WorkItemLinkRowDto>(`${workItemPath(workItemId)}/work-item-links`, request);

export const editLinkCard = (id: string, request: EditLinkCardRequest) =>
  api.patch<LinkCardDto>(`/api/link-cards/${encodeURIComponent(id)}`, request);

/** Removes a URL card or a work item link row (both are link ids). */
export const archiveLinkCard = (id: string) =>
  api.post<void>(`/api/link-cards/${encodeURIComponent(id)}/archive`);

/** Reads again the cards whose live state is due, and answers the whole list. */
export const refreshLinkCards = (workItemId: string) =>
  api.post<LinkCardsDto>(`${workItemPath(workItemId)}/link-cards/refresh`);

/** What uploading these files would do (add, replace, too large, over the item's total), before any upload. */
export const checkUploads = (workItemId: string, request: AttachmentCheckRequest) =>
  api.post<AttachmentCheckDto>(`${workItemPath(workItemId)}/attachments/check`, request);

export const fetchAttachmentMarkdown = (attachmentId: string) =>
  api.get<AttachmentMarkdownDto>(`/api/attachments/${encodeURIComponent(attachmentId)}/markdown`);

/** Markdown rendered by the server; with `workItemId`, its images resolve against that item's attachments. */
export const renderMarkdown = (markdown: string, workItemId?: string) =>
  api.post<RenderedMarkdownDto>('/api/markdown/render', { markdown, workItemId });

/** The item's link cards, read again when someone else's write changes them (their `linksChanged`). */
export function useLinkCards(projectId: string, itemKey: string, workItemId: string | undefined) {
  const queryClient = useQueryClient();
  useEffect(() => {
    if (workItemId === undefined) return undefined;
    return registerInvalidation('linksChanged', (event) => {
      if (event.workItemId === workItemId) {
        void queryClient.invalidateQueries({ queryKey: linkCardsKey(projectId, itemKey) });
      }
    });
  }, [projectId, itemKey, workItemId, queryClient]);
  return useQuery({
    queryKey: linkCardsKey(projectId, itemKey),
    queryFn: () => fetchLinkCards(workItemId ?? ''),
    enabled: workItemId !== undefined,
  });
}

/** A Markdown attachment's page, read again when the item's attachments change (a replace or a removal). */
export function useAttachmentMarkdown(projectId: string, attachmentId: string) {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: attachmentMarkdownKey(projectId, attachmentId),
    queryFn: () => fetchAttachmentMarkdown(attachmentId),
  });
  const workItemId = query.data?.workItem.id;
  useEffect(() => {
    if (workItemId === undefined) return undefined;
    return registerInvalidation('attachmentsChanged', (event) => {
      if (event.workItemId === workItemId) {
        void queryClient.invalidateQueries({
          queryKey: attachmentMarkdownKey(projectId, attachmentId),
        });
      }
    });
  }, [projectId, attachmentId, workItemId, queryClient]);
  return query;
}

/** The server's render of `markdown`, with its images resolved against `workItemId`'s attachments when given. */
export const useItemMarkdown = (
  markdown: string,
  workItemId: string | undefined,
  enabled: boolean,
) =>
  useQuery({
    queryKey: itemMarkdownKey(markdown, workItemId),
    queryFn: () => renderMarkdown(markdown, workItemId),
    enabled,
    staleTime: Infinity,
  });
