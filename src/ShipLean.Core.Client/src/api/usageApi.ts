import { api } from './client';

/** POST api/usage/screen: one screen view (mode: the backlog mode on a backlog screen). The server answers 204. */
export function postScreenView(body: {
  screen: string;
  projectId: string | null;
  mode?: string;
}): Promise<void> {
  return api.post<void>('/api/usage/screen', body);
}

/** POST api/projects/{id}/usage/backlog-sort: a click on a backlog column title, and the sort it led to ('off' = rank order). The server answers 204. */
export function postBacklogSort(
  projectId: string,
  body: { screen: string; column: string; direction: 'asc' | 'desc' | 'off' },
): Promise<void> {
  return api.post<void>(`/api/projects/${encodeURIComponent(projectId)}/usage/backlog-sort`, body);
}

/** POST api/projects/{id}/usage/timeline-opened: a Feature timeline opened, from the Feature page ('feature') or a link. The server answers 204. */
export function postTimelineOpened(projectId: string, from: 'feature' | 'link'): Promise<void> {
  return api.post<void>(`/api/projects/${encodeURIComponent(projectId)}/usage/timeline-opened`, {
    from,
  });
}

/** POST api/projects/{id}/usage/timeline-bar-selected: a bar selected on a Feature timeline. The server answers 204. */
export function postTimelineBarSelected(projectId: string): Promise<void> {
  return api.post<void>(
    `/api/projects/${encodeURIComponent(projectId)}/usage/timeline-bar-selected`,
    {},
  );
}
