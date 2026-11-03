// Fetchers and mutations for every retrospective route (SHIP-82 contract C6). The shapes are in retros.ts.
import { api } from './client';
import type {
  RetroActionAddPoints,
  RetroActionDetailDto,
  RetroActionDialogDto,
  RetroActionNew,
  RetroActionPut,
  RetroDecision,
  RetroFeaturesDto,
  RetroItemDto,
  RetroNotNeededPut,
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

/** The insights range: the last 6 or 3 sprints, all, or the last 6 or 12 weeks. */
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
  noSprint?: boolean;
  decision?: RetroDecision;
  unthemed?: boolean;
}

/** What the action dialog is opened on; `pointIds` repeat as `point`. */
export interface RetroActionDialogQuery {
  pointIds?: string[];
  themeId?: string;
  itemId?: string;
  sprintId?: string;
}

const projectPath = (projectId: string) => `/api/projects/${encodeURIComponent(projectId)}`;
const actionPath = (workItemId: string) => `/api/retros/actions/${encodeURIComponent(workItemId)}`;
const proposalPath = (proposalId: string) =>
  `/api/retros/theme-merge-proposals/${encodeURIComponent(proposalId)}`;
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
  if (query.noSprint) search.set('noSprint', 'true');
  if (query.decision) search.set('decision', query.decision);
  if (query.unthemed) search.set('unthemed', 'true');
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

export const fetchRetroFeatures = (projectId: string) =>
  api.get<RetroFeaturesDto>(`${projectPath(projectId)}/retros/features`);

export const fetchRetroItem = (projectId: string, itemKey: string) =>
  api.get<RetroItemDto>(`${projectPath(projectId)}/retros/items/${encodeURIComponent(itemKey)}`);

export function actionDialogQueryString(query: RetroActionDialogQuery): string {
  const search = new URLSearchParams();
  for (const id of query.pointIds ?? []) search.append('point', id);
  if (query.themeId) search.set('theme', query.themeId);
  if (query.itemId) search.set('item', query.itemId);
  if (query.sprintId) search.set('sprint', query.sprintId);
  const text = search.toString();
  return text === '' ? '' : `?${text}`;
}

export const fetchRetroActionDialog = (projectId: string, query: RetroActionDialogQuery) =>
  api.get<RetroActionDialogDto>(
    `${projectPath(projectId)}/retros/action-dialog${actionDialogQueryString(query)}`,
  );

export const fetchRetroAction = (workItemId: string) =>
  api.get<RetroActionDetailDto>(actionPath(workItemId));

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

export const markPointNotNeeded = (pointId: string, put: RetroNotNeededPut) =>
  api.post<RetroPointDto>(`${pointPath(pointId)}/not-needed`, put);

export const undoPointNotNeeded = (pointId: string) =>
  api.post<RetroPointDto>(`${pointPath(pointId)}/undo-not-needed`);

export const newRetroAction = (projectId: string, create: RetroActionNew) =>
  api.post<RetroActionDetailDto>(`${projectPath(projectId)}/retros/actions`, create);

export const addPointsToRetroAction = (workItemId: string, add: RetroActionAddPoints) =>
  api.post<RetroActionDetailDto>(`${actionPath(workItemId)}/points`, add);

export const putRetroAction = (workItemId: string, put: RetroActionPut) =>
  api.put<RetroActionDetailDto>(actionPath(workItemId), put);

export const mergeThemeProposal = (proposalId: string) =>
  api.post<void>(`${proposalPath(proposalId)}/merge`);

export const keepThemeProposalApart = (proposalId: string) =>
  api.post<void>(`${proposalPath(proposalId)}/keep-apart`);

export const renameRetroTheme = (themeId: string, rename: RetroThemeRename) =>
  api.put<RetroThemeRefDto>(`/api/retros/themes/${encodeURIComponent(themeId)}`, rename);

export const mergeRetroThemes = (projectId: string, merge: RetroThemeMerge) =>
  api.post<void>(`${projectPath(projectId)}/retros/themes/merge`, merge);
