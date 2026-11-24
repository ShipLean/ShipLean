import { api } from './client';
import type { PromptKey } from './prompts';

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

/** POST api/projects/{id}/usage/board-filter-opened: the Filter control of this board level opened (the sheet on a phone, the bar elsewhere). The server answers 204. */
export function postBoardFilterOpened(
  projectId: string,
  level: 'features' | 'stories' | 'epics',
): Promise<void> {
  return api.post<void>(
    `/api/projects/${encodeURIComponent(projectId)}/usage/board-filter-opened`,
    { level },
  );
}

/** POST api/projects/{id}/usage/board-health-filtered: the Features board's Health filter changed to this ('none' = Not set, 'any' = cleared). The server answers 204. */
export function postBoardHealthFiltered(
  projectId: string,
  health: 'onTrack' | 'atRisk' | 'blocked' | 'none' | 'any',
): Promise<void> {
  return api.post<void>(
    `/api/projects/${encodeURIComponent(projectId)}/usage/board-health-filtered`,
    { health },
  );
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

/** POST api/projects/{id}/usage/inspection-metrics-viewed: an Inspection view shown. The server answers 204. */
export function postInspectionMetricsViewed(
  projectId: string,
  view: 'overview' | 'leadtime' | 'period' | 'quality' | 'cost' | 'json',
): Promise<void> {
  return api.post<void>(
    `/api/projects/${encodeURIComponent(projectId)}/usage/inspection-metrics-viewed`,
    { view },
  );
}

/** POST api/projects/{id}/usage/inspection-metrics-period-opened: a sprint opened on Inspection, by kind (not its id). The server answers 204. */
export function postInspectionMetricsPeriodOpened(
  projectId: string,
  period: 'finished' | 'running',
): Promise<void> {
  return api.post<void>(
    `/api/projects/${encodeURIComponent(projectId)}/usage/inspection-metrics-period-opened`,
    { period },
  );
}

/** POST api/projects/{id}/usage/inspection-metrics-task-opened: a task opened on Inspection, from the rail, Longest tasks, a chart dot or Cost's most tokens. The server answers 204. */
export function postInspectionMetricsTaskOpened(
  projectId: string,
  from: 'rail' | 'longest' | 'chart' | 'mostTokens',
): Promise<void> {
  return api.post<void>(
    `/api/projects/${encodeURIComponent(projectId)}/usage/inspection-metrics-task-opened`,
    { from },
  );
}

/** POST api/projects/{id}/usage/retro-theme-sheet-opened: a sheet opened on the retro insights Themes tab: one theme's ('theme') or the Unthemed one ('unthemed'). The server answers 204. */
export function postRetroThemeSheetOpened(
  projectId: string,
  sheet: 'theme' | 'unthemed',
): Promise<void> {
  return api.post<void>(
    `/api/projects/${encodeURIComponent(projectId)}/usage/retro-theme-sheet-opened`,
    { sheet },
  );
}

/** POST api/projects/{id}/usage/prompts-opened: the Ways of working page opened. The server answers 204. */
export function promptsOpened(projectId: string): Promise<void> {
  return api.post<void>(`/api/projects/${encodeURIComponent(projectId)}/usage/prompts-opened`, {});
}

/** POST api/projects/{id}/usage/prompt-customize-opened: the editor opened on this prompt (Customize, Edit or Apply and edit). The server answers 204. */
export function promptCustomizeOpened(projectId: string, prompt: PromptKey): Promise<void> {
  return api.post<void>(
    `/api/projects/${encodeURIComponent(projectId)}/usage/prompt-customize-opened`,
    { prompt },
  );
}

/** POST api/projects/{id}/usage/prompts-search-picked: a prompt opened from the Ways of working header search (click or Enter). The server answers 204. */
export function promptsSearchPicked(projectId: string, prompt: PromptKey): Promise<void> {
  return api.post<void>(
    `/api/projects/${encodeURIComponent(projectId)}/usage/prompts-search-picked`,
    { prompt },
  );
}
