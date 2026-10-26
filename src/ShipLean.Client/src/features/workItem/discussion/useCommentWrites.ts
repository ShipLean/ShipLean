import { useMutation, useQueryClient } from '@tanstack/react-query';
import { showToast } from '../../../components/toastStore';
import {
  addComment,
  archiveComment,
  editComment,
  supersedeComment,
  workItemCommentsKey,
} from '../workItemApi';

// A comment is its own record and saves at once: these writes refetch the comment list and never touch the item or the
// form's draft, so a comment never makes the form dirty.
export function useCommentWrites(projectId: string, itemKey: string, workItemId: string) {
  const queryClient = useQueryClient();
  const refetch = () =>
    queryClient.invalidateQueries({ queryKey: workItemCommentsKey(projectId, itemKey) });
  return {
    add: useMutation({
      mutationFn: (body: string) => addComment(workItemId, { body }),
      onSuccess: refetch,
    }),
    edit: useMutation({
      mutationFn: (change: { id: string; body: string }) =>
        editComment(change.id, { body: change.body }),
      onSuccess: refetch,
    }),
    supersede: useMutation({
      mutationFn: (id: string) => supersedeComment(id),
      onSuccess: refetch,
      onError: (failure) => showToast(failure.message, { tone: 'danger' }),
    }),
    archive: useMutation({
      mutationFn: (id: string) => archiveComment(id),
      onSuccess: refetch,
    }),
  };
}
