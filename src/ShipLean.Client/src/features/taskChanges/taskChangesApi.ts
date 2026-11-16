import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useState } from 'react';
import { api } from '@shiplean/core/api/client';
import type {
  AddCodeCommentRequest,
  AddTaskCommitRequest,
  ApproveChangesRequest,
  CodeCommentDto,
  ReplyCodeCommentRequest,
  RequestTaskChangesRequest,
  ResolveCodeCommentRequest,
  ReviewOutcomeDto,
  TaskChangesDto,
  TaskCommitsSectionDto,
} from '@shiplean/core/api/taskChanges';
import { registerInvalidation } from '../../realtime';

const base = (workItemId: string) => `/api/work-items/${workItemId}`;
export const commitsPath = (workItemId: string) => `${base(workItemId)}/commits`;
export const changesPath = (workItemId: string, commit: string) =>
  `${base(workItemId)}/changes?commit=${encodeURIComponent(commit)}`;
export const codeCommentsPath = (workItemId: string) => `${base(workItemId)}/code-comments`;

const rootKey = (projectId: string, workItemId: string) =>
  ['projects', projectId, 'task-changes', workItemId] as const;
export const sectionKey = (projectId: string, workItemId: string) =>
  [...rootKey(projectId, workItemId), 'section'] as const;
export const pageKey = (projectId: string, workItemId: string, commit: string) =>
  [...rootKey(projectId, workItemId), 'page', commit] as const;

/** The Task page's Changes section. `enabled` stays false until the work item's id is known. */
export function useTaskCommitsSection(projectId: string, workItemId: string | undefined) {
  const queryClient = useQueryClient();
  const id = workItemId ?? '';
  const query = useQuery({
    queryKey: sectionKey(projectId, id),
    queryFn: () => api.get<TaskCommitsSectionDto>(commitsPath(id)),
    enabled: workItemId !== undefined,
  });
  useEffect(() => {
    if (workItemId === undefined) return undefined;
    const refetch = (event: { workItemId: string }) => {
      if (event.workItemId === workItemId) {
        void queryClient.invalidateQueries({ queryKey: sectionKey(projectId, workItemId) });
      }
    };
    const offs = [
      registerInvalidation('commitsChanged', refetch),
      registerInvalidation('codeCommentsChanged', refetch),
    ];
    return () => offs.forEach((off) => off());
  }, [projectId, workItemId, queryClient]);
  return query;
}

/**
 * The Changes page for `commit` ("all" or a work_item_commit id). A teammate's new commit does not refetch: it sets
 * `newCommit` so the page can offer a reload and the diff under the reader's eyes does not move. `reload` refetches.
 * Code comment events refetch at once.
 */
export function useTaskChanges(projectId: string, workItemId: string | undefined, commit: string) {
  const queryClient = useQueryClient();
  const [newCommit, setNewCommit] = useState(false);
  const id = workItemId ?? '';
  const query = useQuery({
    queryKey: pageKey(projectId, id, commit),
    queryFn: () => api.get<TaskChangesDto>(changesPath(id, commit)),
    enabled: workItemId !== undefined,
    staleTime: Infinity,
  });
  useEffect(() => {
    if (workItemId === undefined) return undefined;
    const offs = [
      registerInvalidation('commitsChanged', (event) => {
        if (event.workItemId === workItemId) setNewCommit(true);
      }),
      registerInvalidation('codeCommentsChanged', (event) => {
        if (event.workItemId === workItemId) {
          void queryClient.invalidateQueries({
            queryKey: [...rootKey(projectId, workItemId), 'page'],
          });
        }
      }),
    ];
    return () => offs.forEach((off) => off());
  }, [projectId, workItemId, queryClient]);
  const reload = useCallback(() => {
    setNewCommit(false);
    void queryClient.invalidateQueries({ queryKey: rootKey(projectId, id) });
  }, [queryClient, projectId, id]);
  return { query, newCommit, reload };
}

function useRefreshing(projectId: string, workItemId: string) {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: rootKey(projectId, workItemId) });
}

export function useAddTaskCommit(projectId: string, workItemId: string) {
  const refresh = useRefreshing(projectId, workItemId);
  return useMutation({
    mutationFn: (request: AddTaskCommitRequest) =>
      api.post<TaskCommitsSectionDto>(commitsPath(workItemId), request),
    onSuccess: refresh,
  });
}

export function useAddCodeComment(projectId: string, workItemId: string) {
  const refresh = useRefreshing(projectId, workItemId);
  return useMutation({
    mutationFn: (request: AddCodeCommentRequest) =>
      api.post<CodeCommentDto>(codeCommentsPath(workItemId), request),
    onSuccess: refresh,
  });
}

export function useReplyToCodeComment(projectId: string, workItemId: string) {
  const refresh = useRefreshing(projectId, workItemId);
  return useMutation({
    mutationFn: (vars: { commentId: string } & ReplyCodeCommentRequest) =>
      api.post<CodeCommentDto>(`${codeCommentsPath(workItemId)}/${vars.commentId}/replies`, {
        body: vars.body,
      }),
    onSuccess: refresh,
  });
}

export function useResolveCodeComment(projectId: string, workItemId: string) {
  const refresh = useRefreshing(projectId, workItemId);
  return useMutation({
    mutationFn: (vars: { commentId: string } & ResolveCodeCommentRequest) =>
      api.post<void>(`${codeCommentsPath(workItemId)}/${vars.commentId}/resolve`, {
        resolved: vars.resolved,
      }),
    onSuccess: refresh,
  });
}

/** Approve answers `conflict` when a newer commit arrived than `headCommitId`: the dialog says to reload. */
export function useApproveChanges(projectId: string, workItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ApproveChangesRequest) =>
      api.post<ReviewOutcomeDto>(`${base(workItemId)}/changes/approve`, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects', projectId] }),
  });
}

export function useRequestChanges(projectId: string, workItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RequestTaskChangesRequest) =>
      api.post<ReviewOutcomeDto>(`${base(workItemId)}/changes/request`, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects', projectId] }),
  });
}
