// Comment fetchers. The shapes are in comments.ts.
import { api } from './client';
import type { CommentDto } from './comments';

export const commentKeys = {
  list: (itemId: string) => ['comments', itemId] as const,
};

export const listComments = (itemId: string) =>
  api.get<CommentDto[]>(`/api/work-items/${encodeURIComponent(itemId)}/comments`);

export const addComment = (itemId: string, body: string) =>
  api.post<CommentDto>(`/api/work-items/${encodeURIComponent(itemId)}/comments`, { body });

/** The Undo of a note: the comment is archived, not deleted. 204. */
export const archiveComment = (commentId: string) =>
  api.post<void>(`/api/comments/${encodeURIComponent(commentId)}/archive`);
