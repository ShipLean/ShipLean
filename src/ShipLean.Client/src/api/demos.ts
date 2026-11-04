// The sprint demo, its project settings, brand and YouTube wire shapes, and every demo route. C#: ShipLean.Contracts.Demos.
// Round-tripped by demos.test.ts over TestSupport/DtoFixtures/demos. Labels are made by the server; the client formats nothing.
import { api, sendForm } from './client';
import type { AgentStartBlock } from './agentSessions';
import type { StateRef } from './refs';

export const SPRINT_DEMO_STATUSES = [
  'none',
  'closable',
  'requested',
  'working',
  'ready',
  'failed',
  'blocked',
] as const;
export type SprintDemoStatus = (typeof SPRINT_DEMO_STATUSES)[number];

export const YOU_TUBE_DEMO_STATES = ['none', 'pending', 'published', 'failed'] as const;
export type YouTubeDemoState = (typeof YOU_TUBE_DEMO_STATES)[number];

export const YOU_TUBE_VISIBILITIES = ['public', 'unlisted', 'private'] as const;
export type YouTubeVisibility = (typeof YOU_TUBE_VISIBILITIES)[number];

export const YOU_TUBE_CONNECTION_STATUSES = [
  'off',
  'notConnected',
  'pending',
  'connected',
] as const;
export type YouTubeConnectionStatus = (typeof YOU_TUBE_CONNECTION_STATUSES)[number];

/** Null until the sprint has a BAU item. */
export interface SprintDemoRunDto {
  itemKey: string;
  itemUrl: string;
  sessionState: string | null;
  requestedLabel: string;
  uploadExpiresLabel: string | null;
}

export interface SprintDemoVideoDto {
  attachmentId: string;
  /** `/api/attachments/{id}/content`. */
  contentUrl: string;
  name: string;
  sizeBytes: number;
  durationSeconds: number;
  durationLabel: string;
  deliveredLabel: string;
}

export interface SprintDemoChapterDto {
  title: string;
  startSeconds: number;
  startLabel: string;
  featureKey: string | null;
}

export interface SprintDemoFeatureDto {
  key: string;
  title: string;
  state: StateRef;
  url: string;
}

export interface SprintDemoYouTubeDto {
  state: YouTubeDemoState;
  watchUrl: string | null;
  embedUrl: string | null;
  privacy: YouTubeVisibility | null;
  failureMessage: string | null;
}

export interface SprintDemoActionsDto {
  canCreate: boolean;
  createBlockedMessage: string | null;
  canRetryYouTube: boolean;
}

export interface SprintDemoDto {
  sprintId: string;
  sprintName: string;
  status: SprintDemoStatus;
  statusMessage: string | null;
  run: SprintDemoRunDto | null;
  /** Set whenever a video exists, in any status. */
  video: SprintDemoVideoDto | null;
  chapters: SprintDemoChapterDto[];
  features: SprintDemoFeatureDto[];
  scriptHtml: string | null;
  youTube: SprintDemoYouTubeDto;
  actions: SprintDemoActionsDto;
}

export interface DemoAgentCheckDto {
  ready: boolean;
  block: AgentStartBlock | null;
  message: string;
}

export interface DemoDefaultTokenDto {
  name: string;
  value: string;
}

export interface ProjectDemoBrandDto {
  /** Null means ShipLean's logo. */
  logoUrl: string | null;
  logoContentType: string | null;
  tokens: string;
  defaultTokens: DemoDefaultTokenDto[];
}

export interface YouTubeProjectDto {
  status: YouTubeConnectionStatus;
  channelTitle: string | null;
  visibility: YouTubeVisibility | null;
  checkedLabel: string | null;
  refusal: string | null;
}

export interface ProjectDemoSettingsDto {
  automatic: boolean;
  environment: string;
  agentCheck: DemoAgentCheckDto;
  brand: ProjectDemoBrandDto;
  youTube: YouTubeProjectDto;
}

export interface ProjectDemoSettingsPut {
  automatic: boolean;
  environment: string | null;
}

export interface DemoTokensPut {
  tokens: string | null;
}

export interface YouTubeStartDto {
  url: string;
}

export interface YouTubeCheckRequest {
  visibility: YouTubeVisibility;
}

export interface YouTubeDisconnectDto {
  revoked: boolean;
  message: string | null;
}

export interface DemoChapterJson {
  title: string;
  startSeconds: number;
  featureKey: string | null;
}

/** The `sprint-demo-chapters.json` shape. */
export interface DemoChaptersJson {
  durationSeconds: number;
  chapters: DemoChapterJson[];
}

/** What the prompt and `create_sprint_demo` carry (server side only; pinned here by its fixtures). */
export interface DemoBrand {
  logoIsDefault: boolean;
  logoSvg: string | null;
  logoUrl: string | null;
  logoContentType: string | null;
  logoUrlExpiresAt: string | null;
  tokensAreDefault: boolean;
  tokensCss: string;
  logoFallbackReason: string | null;
}

export const fetchSprintDemo = (sprintId: string) =>
  api.get<SprintDemoDto>(`/api/sprints/${sprintId}/demo`);
export const createSprintDemo = (sprintId: string) =>
  api.post<SprintDemoDto>(`/api/sprints/${sprintId}/demo`);
export const retryYouTube = (sprintId: string) =>
  api.post<SprintDemoDto>(`/api/sprints/${sprintId}/demo/youtube/retry`);

export const fetchDemoSettings = (projectId: string) =>
  api.get<ProjectDemoSettingsDto>(`/api/projects/${projectId}/demo-settings`);
export const saveDemoSettings = (projectId: string, put: ProjectDemoSettingsPut) =>
  api.put<ProjectDemoSettingsDto>(`/api/projects/${projectId}/demo-settings`, put);

export function uploadDemoLogo(projectId: string, file: File): Promise<ProjectDemoBrandDto> {
  const form = new FormData();
  form.append('file', file, file.name);
  return sendForm<ProjectDemoBrandDto>(
    'PUT',
    `/api/projects/${projectId}/demo-brand/logo`,
    form,
    'That logo is over 1 MB. Pick a smaller one',
  );
}
export const removeDemoLogo = (projectId: string) =>
  api.delete<ProjectDemoBrandDto>(`/api/projects/${projectId}/demo-brand/logo`);
export const saveDemoTokens = (projectId: string, put: DemoTokensPut) =>
  api.put<ProjectDemoBrandDto>(`/api/projects/${projectId}/demo-brand/tokens`, put);

export const startYouTube = (projectId: string) =>
  api.post<YouTubeStartDto>(`/api/projects/${projectId}/youtube/start`);
export const checkYouTube = (projectId: string, request: YouTubeCheckRequest) =>
  api.post<YouTubeProjectDto>(`/api/projects/${projectId}/youtube/check`, request);
export const disconnectYouTube = (projectId: string) =>
  api.post<YouTubeDisconnectDto>(`/api/projects/${projectId}/youtube/disconnect`);
