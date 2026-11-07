import { api } from './client';

/** POST api/usage/screen: one screen view (mode: the backlog mode on a backlog screen). The server answers 204. */
export function postScreenView(body: {
  screen: string;
  projectId: string | null;
  mode?: string;
}): Promise<void> {
  return api.post<void>('/api/usage/screen', body);
}
