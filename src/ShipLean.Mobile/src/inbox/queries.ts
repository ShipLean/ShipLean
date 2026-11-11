import { getAnswered, getInbox, inboxKeys } from '@shiplean/core/api/inbox';
import { skipToken, useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';

// The project's Inbox; waits until the project id is known.
export const useInboxQuery = (projectId: string | undefined) =>
  useQuery({
    queryKey: inboxKeys.inbox(projectId ?? ''),
    queryFn: projectId === undefined ? skipToken : () => getInbox(projectId),
  });

// What the person answered in the project; waits until the project id is known.
export const useAnsweredQuery = (projectId: string | undefined) =>
  useQuery({
    queryKey: inboxKeys.answered(projectId ?? ''),
    queryFn: projectId === undefined ? skipToken : () => getAnswered(projectId),
  });

// Refreshes the project's Inbox and Answered (the Inbox key is the prefix of both). The function is stable.
export function useInvalidateInbox(): (projectId: string) => Promise<void> {
  const queryClient = useQueryClient();
  return useCallback(
    (projectId: string) => queryClient.invalidateQueries({ queryKey: inboxKeys.inbox(projectId) }),
    [queryClient],
  );
}
