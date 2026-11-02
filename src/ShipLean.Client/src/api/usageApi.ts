import { api } from './client';

/** POST api/usage/screen: one screen view. The server answers 204. */
export function postScreenView(body: { screen: string; projectId: string | null }): Promise<void> {
  return api.post<void>('/api/usage/screen', body);
}
