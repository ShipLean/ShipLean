import { ApiRequestError } from '../../../../api/client';

/** The server's own words for a refusal; anything else is a network failure. */
export const errorText = (error: unknown, fallback: string) =>
  error instanceof ApiRequestError ? error.message : fallback;
