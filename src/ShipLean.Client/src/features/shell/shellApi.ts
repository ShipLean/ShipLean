import { api } from '../../api/client';
import { fetchPreference, preferenceKey } from '../../queries/shared';

export const recentProjectsKey = preferenceKey('recentProjects');

export const RECENT_LIMIT = 5;

// The ids of the projects opened lately, most recent first; none when nothing is saved (the endpoint answers 204).
export async function fetchRecentProjectIds(): Promise<string[]> {
  const saved = await fetchPreference('recentProjects', []);
  return Array.isArray(saved) ? saved.filter((id): id is string => typeof id === 'string') : [];
}

export const saveRecentProjectIds = (ids: string[]) =>
  api.put<void>('/api/me/preferences/recentProjects', ids);

export const signOut = () => api.post<void>('/api/auth/logout');

// Opening a project moves it to the front; the list keeps each project once and at most RECENT_LIMIT.
export function withRecent(ids: string[], projectId: string): string[] {
  return [projectId, ...ids.filter((id) => id !== projectId)].slice(0, RECENT_LIMIT);
}
