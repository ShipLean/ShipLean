import { api } from './client';

/** POST api/usage/screen: one screen view. The server answers 204. */
export function postScreenView(body: { screen: string; projectId: string | null }): Promise<void> {
  return api.post<void>('/api/usage/screen', body);
}

/** POST api/projects/{id}/usage/backlog-sort: a click on a backlog column title, and the sort it led to ('off' = rank order). The server answers 204. */
export function postBacklogSort(
  projectId: string,
  body: { screen: string; column: string; direction: 'asc' | 'desc' | 'off' },
): Promise<void> {
  return api.post<void>(`/api/projects/${encodeURIComponent(projectId)}/usage/backlog-sort`, body);
}
