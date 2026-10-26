import { api } from '../api/client';
import type { BoardSummaryDto } from '../api/boards';
import type { MeDto } from '../api/me';
import type { MemberDto, ProjectDto, ProjectSummaryDto } from '../api/projects';
import type { UserPreferenceDto } from '../api/preferences';
import type { SprintDto } from '../api/sprints';

// The react-query keys every component shares, with the fetcher each one stands for: one fetch serves them all, and
// one invalidation reaches them all. Anything else is keyed under ['projects', projectId, …] (project data) or the
// name of its area. Nobody writes these arrays inline.
//
//   ['me']                                  GET /api/me
//   ['projects']                            GET /api/projects
//   ['projects', 'by-key', key]             GET /api/projects/by-key/{key}
//   ['projects', projectId, 'sprints']      GET /api/projects/{id}/sprints
//   ['projects', projectId, 'members']      GET /api/projects/{id}/members
//   ['projects', projectId, 'boards']       GET /api/projects/{id}/boards
//   ['preferences', key]                    GET /api/me/preferences/{key}
//
// The cache entry of ['preferences', key] holds the preference's value (the parsed JSON, `UserPreferenceDto.json`),
// never the DTO. The endpoint answers 204 when nothing is saved; that maps to the empty value the reader passes
// (fetchPreference), so react-query never holds `undefined`.
export const meKey = ['me'] as const;
export const projectsKey = ['projects'] as const;
export const projectByKeyKey = (key: string) => ['projects', 'by-key', key] as const;
export const projectSprintsKey = (projectId: string) => ['projects', projectId, 'sprints'] as const;
export const projectMembersKey = (projectId: string) => ['projects', projectId, 'members'] as const;
export const projectBoardsKey = (projectId: string) => ['projects', projectId, 'boards'] as const;
export const preferenceKey = (key: string) => ['preferences', key] as const;

export const fetchMe = () => api.get<MeDto>('/api/me');
export const fetchProjects = () => api.get<ProjectSummaryDto[]>('/api/projects');
export const fetchProjectByKey = (key: string) =>
  api.get<ProjectDto>(`/api/projects/by-key/${encodeURIComponent(key)}`);
export const fetchSprints = (projectId: string) =>
  api.get<SprintDto[]>(`/api/projects/${encodeURIComponent(projectId)}/sprints`);
export const fetchMembers = (projectId: string) =>
  api.get<MemberDto[]>(`/api/projects/${encodeURIComponent(projectId)}/members`);
export const fetchBoards = (projectId: string) =>
  api.get<BoardSummaryDto[]>(`/api/projects/${encodeURIComponent(projectId)}/boards`);

// The value of a preference; `empty` when nothing is saved (204).
export async function fetchPreference(key: string, empty: unknown): Promise<unknown> {
  const saved = await api.get<UserPreferenceDto | undefined>(
    `/api/me/preferences/${encodeURIComponent(key)}`,
  );
  return saved === undefined ? empty : saved.json;
}
