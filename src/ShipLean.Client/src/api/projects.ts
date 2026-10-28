// Projects, members, states and the instance. C#: ShipLean.Contracts.Projects and ShipLean.Contracts.Instance. Nothing is
// omitted: absent values are null.
import type { StateCategory, UserRef, WorkItemTypeId } from './refs';
import type { SprintCreate } from './sprints';
import type { UserStatus } from './users';

export const PROJECT_ROLES = ['member', 'admin'] as const;
export type ProjectRole = (typeof PROJECT_ROLES)[number];

export const KEY_CHECK_REASONS = ['taken', 'takenByAnotherProject', 'invalid'] as const;
export type KeyCheckReason = (typeof KEY_CHECK_REASONS)[number];

export const PROJECT_CREATIONS = ['everyone', 'admins'] as const;
export type ProjectCreation = (typeof PROJECT_CREATIONS)[number];

export interface ProjectSummaryDto {
  id: string;
  key: string;
  name: string;
  role: ProjectRole;
  memberCount: number;
}

export interface ProjectDto {
  id: string;
  key: string;
  name: string;
  description: string;
  /** The caller's role. */
  role: ProjectRole;
  memberCount: number;
  /** ISO weekdays, 1 = Monday .. 7 = Sunday. */
  workingDays: number[];
  velocityWindow: number;
  velocityOverride: number | null;
  nextItemNumber: number;
}

export interface MemberDto {
  user: UserRef;
  email: string;
  role: ProjectRole;
  status: UserStatus;
}

export interface StateDto {
  id: string;
  type: WorkItemTypeId;
  name: string;
  category: StateCategory;
  order: number;
  itemCount: number;
  /** Moving an item into this state opens an agent session. */
  startsAgent: boolean;
}

/** A connection a project admin can pick; `archived` marks the project's own choice after it was archived. */
export interface AgentConnectionOption {
  id: string;
  name: string;
  archived: boolean;
}

export interface ProjectGeneralSettings {
  key: string;
  name: string;
  description: string;
  workingDays: number[];
  agentConnectionId: string | null;
  repoUrl: string | null;
  /** As saved: null means main. */
  repoBaseBranch: string | null;
  agentConnections: AgentConnectionOption[];
}

export interface ProjectForecastSettings {
  velocityWindow: number;
  velocityOverride: number | null;
}

export interface ProjectSettingsDto {
  general: ProjectGeneralSettings;
  members: MemberDto[];
  sprints: ProjectForecastSettings;
  states: StateDto[];
}

export interface ProjectGeneralPut {
  name: string;
  description: string;
  workingDays: number[];
  /** Null (or blank text) clears the agent fields. */
  agentConnectionId: string | null;
  repoUrl: string | null;
  repoBaseBranch: string | null;
}

export interface MemberPut {
  email: string;
  role: ProjectRole;
}

/** The settings page as saved: the full members list (a member left out is removed) and the States section's changes. */
export interface ProjectSettingsPut {
  general: ProjectGeneralPut;
  members: MemberPut[];
  sprints: ProjectForecastSettings;
  states: StateChanges;
}

export interface ProjectCreate {
  name: string;
  key: string;
  copyFromProjectId: string | null;
  memberEmails: string[];
  workingDays: number[];
  /** Null = no Sprint 1. */
  firstSprint: SprintCreate | null;
}

export interface KeyCheckDto {
  key: string;
  available: boolean;
  reason: KeyCheckReason | null;
  alternatives: string[];
}

/** The id is chosen by the client so reorder and remove in the same request can refer to the new state. */
export interface StateAdd {
  id: string;
  type: WorkItemTypeId;
  name: string;
  category: StateCategory;
  startsAgent: boolean;
}

/** Rename and/or recategorise; the state keeps its id and items. */
export interface StateUpdate {
  stateId: string;
  name: string;
  category: StateCategory;
  /** Left out = unchanged. */
  startsAgent?: boolean;
}

export interface StateReorder {
  type: WorkItemTypeId;
  stateIds: string[];
}

export interface StateRemove {
  stateId: string;
  targetStateId: string;
}

export interface StateChanges {
  add: StateAdd[];
  update: StateUpdate[];
  reorder: StateReorder[];
  remove: StateRemove[];
}

export interface InstanceSettingsDto {
  name: string;
  baseUrl: string;
  projectCreation: ProjectCreation;
}

export interface InstanceProjectDto {
  id: string;
  key: string;
  name: string;
  memberCount: number;
  admins: UserRef[];
  hasActiveAdmin: boolean;
  itemCount: number;
  yourRole: ProjectRole | null;
}
