import { ApiRequestError } from '@shiplean/core/api/client';
import type { AppAction, PendingFailure } from './types';

export interface SendError {
  error: string;
  failure: PendingFailure;
  retryable: boolean;
}

// No response at all: the request never reached ShipLean, or its answer never came back.
export const NO_ANSWER = 'No answer from ShipLean.';

// Fetch rejects with a TypeError when no response came back, and with an AbortError when the request was cut off. Any
// other error that isn't an ApiRequestError is a bug in an executor, not a missing answer.
export function isNoAnswer(e: unknown): boolean {
  if (e instanceof TypeError) return true;
  return typeof e === 'object' && e !== null && (e as { name?: unknown }).name === 'AbortError';
}

// The one wording for a send that didn't go through, whatever the area.
export function sendError(e: unknown, a: AppAction): SendError {
  if (!(e instanceof ApiRequestError)) {
    if (isNoAnswer(e)) return { error: NO_ANSWER, failure: 'network', retryable: true };
    return { error: "Didn't send.", failure: 'server', retryable: false };
  }
  const item = a.key ?? 'the item';
  if (e.status === 409)
    return {
      error: `Changed on the server. Open ${item} to check.`,
      failure: 'conflict',
      retryable: false,
    };
  if (e.status === 404)
    return {
      error: `${a.key ?? 'The item'} isn't there any more.`,
      failure: 'notFound',
      retryable: false,
    };
  if (e.status === 400) return { error: e.message, failure: 'validation', retryable: false };
  return { error: `Didn't send (${e.status}).`, failure: 'server', retryable: true };
}
