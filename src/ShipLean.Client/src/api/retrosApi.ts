// Fetchers and mutations for every retrospective route (SHIP-82 contract C6). The shapes are in retros.ts.
import { api } from './client';
import type {
  RetroActionCreate,
  RetroActionRefDto,
  RetroInsightsDto,
  RetroPointCreate,
  RetroPointDto,
  RetroPointThemePut,
  RetroSearchDto,
  RetroThemeListDto,
  RetroThemeMerge,
  RetroThemeRefDto,
  RetroThemeRename,
  SprintRetroDto,
  WorkItemRetrosDto,
} from './retros';

/** The insights range: the last 6 or 3 sprints, or all. */
export type RetroRange = RetroInsightsDto['range'];

/** The search filters; each is its query parameter, and absent means no filter. */
export interface RetroSearchQuery {
  q?: string;
  sprint?: string;
  category?: string;
  theme?: string;
  level?: 'stories' | 'features';
  role?: string;
  recurring?: boolean;
  selected?: string;
}

const projectPath = (projectId: string) => `/api/projects/${encodeURIComponent(projectId)}`;
const pointPath = (pointId: string) => `/api/retros/points/${encodeURIComponent(pointId)}`;

export function searchQueryString(query: RetroSearchQuery): string {
  const search = new URLSearchParams();
  if (query.q) search.set('q', query.q);
  if (query.sprint) search.set('sprint', query.sprint);
  if (query.category) search.set('category', query.category);
  if (query.theme) search.set('theme', query.theme);
  if (query.level) search.set('level', query.level);
  if (query.role) search.set('role', query.role);
  if (query.recurring) search.set('recurring', 'true');
  if (query.selected) search.set('selected', query.selected);
  const text = search.toString();
  return text === '' ? '' : `?${text}`;
}

export const fetchSprintRetro = (sprintId: string) =>
  api.get<SprintRetroDto>(`/api/sprints/${encodeURIComponent(sprintId)}/retro`);

export const fetchRetroSearch = (projectId: string, query: RetroSearchQuery) =>
  api.get<RetroSearchDto>(`${projectPath(projectId)}/retros${searchQueryString(query)}`);

export const fetchRetroInsights = (projectId: string, range: RetroRange) =>
  api.get<RetroInsightsDto>(
    `${projectPath(projectId)}/retros/insights?range=${encodeURIComponent(range)}`,
  );

export const fetchRetroThemes = (projectId: string) =>
  api.get<RetroThemeListDto>(`${projectPath(projectId)}/retros/themes`);

export const fetchWorkItemRetros = (workItemId: string) =>
  api.get<WorkItemRetrosDto>(`/api/work-items/${encodeURIComponent(workItemId)}/retros`);

export const createRetroPoint = (projectId: string, create: RetroPointCreate) =>
  api.post<RetroPointDto>(`${projectPath(projectId)}/retros/points`, create);

export const putRetroPointTheme = (pointId: string, put: RetroPointThemePut) =>
  api.put<RetroPointDto>(`${pointPath(pointId)}/theme`, put);

export const archiveRetroPoint = (pointId: string) =>
  api.post<void>(`${pointPath(pointId)}/archive`);

export const createRetroAction = (pointId: string, create: RetroActionCreate) =>
  api.post<RetroActionRefDto>(`${pointPath(pointId)}/action`, create);

export const renameRetroTheme = (themeId: string, rename: RetroThemeRename) =>
  api.put<RetroThemeRefDto>(`/api/retros/themes/${encodeURIComponent(themeId)}`, rename);

export const mergeRetroThemes = (projectId: string, merge: RetroThemeMerge) =>
  api.post<void>(`${projectPath(projectId)}/retros/themes/merge`, merge);
