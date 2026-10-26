import { ApiRequestError } from '../api/client';

const MAX_RETRIES = 3;

// The app's one retry policy (the QueryClient default in main.tsx): an answer from the server that says the request
// itself is wrong (any 4xx: not found, forbidden, validation) is final and shows at once; a network error or a 5xx
// is retried up to three times.
export function retryUnlessClientError(failureCount: number, error: Error): boolean {
  if (error instanceof ApiRequestError && error.status >= 400 && error.status < 500) return false;
  return failureCount < MAX_RETRIES;
}
