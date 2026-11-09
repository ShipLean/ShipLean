import { api } from '../../api/client';
import type { AnswerKind, ChangeDecisionRequest, ItemAsksDto, WaitingDto } from '../../api/asks';
import { workItemKey } from '../workItem/workItemApi';

// Decisions and questions on a work item (K4). Reads return DTOs; every write returns 204 and the caller refetches.

export const asksKey = (projectId: string, itemKey: string) =>
  [...workItemKey(projectId, itemKey), 'asks'] as const;

export const waitingKey = () => ['me', 'waiting'] as const;

const itemPath = (workItemId: string) => `/api/work-items/${encodeURIComponent(workItemId)}`;

export const fetchAsks = (workItemId: string) =>
  api.get<ItemAsksDto>(`${itemPath(workItemId)}/asks`);

/** Numbers no longer open are skipped by the server. */
export const confirmDecisions = (workItemId: string, numbers: number[]) =>
  api.post<void>(`${itemPath(workItemId)}/decisions/confirm`, { numbers });

export interface DecisionChange {
  kind: AnswerKind;
  alternativeIndex: number | null;
  text: string | null;
  note: string | null;
}

export const changeDecision = (workItemId: string, number: number, change: DecisionChange) =>
  api.post<void>(
    `${itemPath(workItemId)}/decisions/${number}/change`,
    change satisfies ChangeDecisionRequest,
  );

export const withdrawDecision = (workItemId: string, number: number) =>
  api.post<void>(`${itemPath(workItemId)}/decisions/${number}/withdraw`);

/** body is Markdown; it is posted to Discussion tagged "On D<n>". */
export const discussDecision = (workItemId: string, number: number, body: string) =>
  api.post<void>(`${itemPath(workItemId)}/decisions/${number}/discuss`, { body });

/** body is Markdown; it is posted to Discussion tagged "Answer to Q<n>". */
export const answerQuestion = (workItemId: string, number: number, body: string) =>
  api.post<void>(`${itemPath(workItemId)}/questions/${number}/answer`, { body });

export const withdrawQuestion = (workItemId: string, number: number) =>
  api.post<void>(`${itemPath(workItemId)}/questions/${number}/withdraw`);

export const fetchWaiting = () => api.get<WaitingDto>('/api/me/waiting');
