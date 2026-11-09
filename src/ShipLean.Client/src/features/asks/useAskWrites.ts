import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { ApiRequestError } from '../../api/client';
import { workItemCommentsKey, workItemHistoryKey } from '../workItem/workItemApi';
import {
  answerQuestion,
  asksKey,
  changeDecision,
  confirmDecisions,
  discussDecision,
  withdrawDecision,
  withdrawQuestion,
  type DecisionChange,
} from './asksApi';

// Every ask write can change the asks, add an ask line to History and post or tag a comment in Discussion, so each one
// refetches all three. A refused write leaves its ApiRequestError on the mutation (error) for the region to show.
export function useAskWrites(projectId: string, itemKey: string, workItemId: string) {
  const queryClient = useQueryClient();
  const refetch = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: asksKey(projectId, itemKey) }),
      queryClient.invalidateQueries({ queryKey: workItemHistoryKey(projectId, itemKey) }),
      queryClient.invalidateQueries({ queryKey: workItemCommentsKey(projectId, itemKey) }),
    ]);
  return {
    confirm: useMutation<void, ApiRequestError, number[]>({
      mutationFn: (numbers) => confirmDecisions(workItemId, numbers),
      onSuccess: refetch,
    }),
    change: useMutation<void, ApiRequestError, { number: number; change: DecisionChange }>({
      mutationFn: ({ number, change }) => changeDecision(workItemId, number, change),
      onSuccess: refetch,
    }),
    withdrawDecision: useMutation<void, ApiRequestError, number>({
      mutationFn: (number) => withdrawDecision(workItemId, number),
      onSuccess: refetch,
    }),
    discuss: useMutation<void, ApiRequestError, { number: number; body: string }>({
      mutationFn: ({ number, body }) => discussDecision(workItemId, number, body),
      onSuccess: refetch,
    }),
    answer: useMutation<void, ApiRequestError, { number: number; body: string }>({
      mutationFn: ({ number, body }) => answerQuestion(workItemId, number, body),
      onSuccess: refetch,
    }),
    withdrawQuestion: useMutation<void, ApiRequestError, number>({
      mutationFn: (number) => withdrawQuestion(workItemId, number),
      onSuccess: refetch,
    }),
  };
}

export type AskWrites = ReturnType<typeof useAskWrites>;
