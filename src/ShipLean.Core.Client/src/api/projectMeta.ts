// The project's states, members and sprints, which every screen's pickers read.
import { api } from './client';
import type { MemberDto, StateDto } from './projects';
import type { SprintDto } from './sprints';

export const projectMetaKeys = {
  states: (projectId: string) => ['projects', projectId, 'states'] as const,
  members: (projectId: string) => ['projects', projectId, 'members'] as const,
  sprints: (projectId: string) => ['projects', projectId, 'sprints'] as const,
};

const projectPath = (projectId: string) => `/api/projects/${encodeURIComponent(projectId)}`;

export const getStates = (projectId: string) =>
  api.get<StateDto[]>(`${projectPath(projectId)}/states`);
export const getMembers = (projectId: string) =>
  api.get<MemberDto[]>(`${projectPath(projectId)}/members`);
export const getSprints = (projectId: string) =>
  api.get<SprintDto[]>(`${projectPath(projectId)}/sprints`);
