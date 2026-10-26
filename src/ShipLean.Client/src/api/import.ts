// Jira import: connections, projects, the mapping, jobs, errors and the log. C#: ShipLean.Contracts.Import. Nothing is omitted:
// absent values are null. The Jira-side records (ShipLean.Contracts.Import.Jira) never reach the browser.
import type { ImportJobProgressJson, ImportJobState, ImportStep } from './persisted';
import type { StateCategory, UserRef } from './refs';

export const JIRA_KINDS = ['cloud', 'server'] as const;
export type JiraKind = (typeof JIRA_KINDS)[number];

export const IMPORT_TARGET_KINDS = ['newProject', 'existingProject'] as const;
export type ImportTargetKind = (typeof IMPORT_TARGET_KINDS)[number];

export const IMPORT_TYPE_TARGETS = ['epic', 'feature', 'story', 'bug', 'task', 'skip'] as const;
export type ImportTypeTarget = (typeof IMPORT_TYPE_TARGETS)[number];

/** Jira's own status categories: To Do, In Progress, Done. */
export const JIRA_STATUS_CATEGORIES = ['new', 'indeterminate', 'done'] as const;
export type JiraStatusCategory = (typeof JIRA_STATUS_CATEGORIES)[number];

export const USER_RESOLUTIONS = ['matched', 'create', 'jiraImportUser', 'existing'] as const;
export type UserResolution = (typeof USER_RESOLUTIONS)[number];

export const IMPORT_WARNING_SEVERITIES = ['info', 'warning'] as const;
export type ImportWarningSeverity = (typeof IMPORT_WARNING_SEVERITIES)[number];

export const IMPORT_MAPPING_SECTIONS = ['types', 'statuses', 'users', 'fields'] as const;
export type ImportMappingSection = (typeof IMPORT_MAPPING_SECTIONS)[number];

export const IMPORT_ERROR_RESOLUTIONS = ['pending', 'retried', 'skipped'] as const;
export type ImportErrorResolution = (typeof IMPORT_ERROR_RESOLUTIONS)[number];

export const IMPORT_LOG_LEVELS = ['info', 'warning', 'error'] as const;
export type ImportLogLevel = (typeof IMPORT_LOG_LEVELS)[number];

/** The token is never returned: only its last four characters. Server: a personal access token, email may be empty. */
export interface JiraConnectionDto {
  id: string;
  projectId: string;
  baseUrl: string;
  kind: JiraKind;
  email: string;
  tokenLastFour: string;
  createdBy: UserRef | null;
  createdAt: string;
  createdLabel: string;
}

/** Saving a connection; the token is write-only. */
export interface JiraConnectionPut {
  baseUrl: string;
  kind: JiraKind;
  email: string;
  token: string;
}

export interface JiraWhoDto {
  displayName: string;
  email: string | null;
}

/** Ok: who is signed in. Not ok: message says what to check. */
export interface JiraTestResultDto {
  ok: boolean;
  who: JiraWhoDto | null;
  message: string | null;
}

export interface ImportedAsDto {
  projectKey: string;
  jobId: string;
  finishedAt: string;
  finishedLabel: string;
}

export interface JiraProjectDto {
  key: string;
  name: string;
  issueCount: number;
  /** The latest finished import of this Jira project; null when never imported. */
  importedAs: ImportedAsDto | null;
}

/** New project (key, name) or an existing empty project (projectId); the unused fields are null. */
export interface ImportTarget {
  kind: ImportTargetKind;
  key: string | null;
  name: string | null;
  projectId: string | null;
}

export interface IssueTypeMappingDto {
  jiraType: string;
  count: number;
  target: ImportTypeTarget;
  note: string | null;
  isNewSinceLastRun: boolean;
}

/** The status lands in the target type's state called targetState; createsState: the run creates it with targetCategory. */
export interface StatusMappingDto {
  jiraType: string;
  jiraStatus: string;
  jiraCategory: JiraStatusCategory;
  count: number;
  targetState: string;
  targetCategory: StateCategory;
  createsState: boolean;
  isNewSinceLastRun: boolean;
}

/** matched and existing carry user; create makes a user without a password; jiraImportUser attributes to the seeded import user. */
export interface UserMappingDto {
  jiraAccountId: string;
  name: string;
  email: string | null;
  references: number;
  resolution: UserResolution;
  user: UserRef | null;
  /** An automation account (Jira Cloud accountType app): never offered Create, defaults to jiraImportUser. */
  isBot: boolean;
  isNewSinceLastRun: boolean;
}

export interface StoryPointsFieldDto {
  fieldId: string;
  name: string;
  populated: number;
  missing: number;
}

export interface ImportFieldsDto {
  storyPointsFieldId: string;
  candidates: StoryPointsFieldDto[];
  includeComponents: boolean;
  includeComments: boolean;
  includeAttachments: boolean;
  includeChangelog: boolean;
}

export interface ImportWarningDto {
  severity: ImportWarningSeverity;
  section: ImportMappingSection;
  message: string;
}

/** What the run would bring in under the current choices; a switched-off option counts 0. */
export interface ImportTotalsDto {
  issues: number;
  comments: number;
  attachments: number;
  revisions: number;
  sprints: number;
  people: number;
}

/** Totals and warnings are computed by the server and ignored on a request. */
export interface ImportMappingDto {
  issueTypes: IssueTypeMappingDto[];
  statuses: StatusMappingDto[];
  users: UserMappingDto[];
  fields: ImportFieldsDto;
  totals: ImportTotalsDto;
  warnings: ImportWarningDto[];
}

export interface ImportStartRequest {
  connectionId: string;
  jiraProjectKey: string;
  target: ImportTarget;
  mapping: ImportMappingDto;
}

/** projectId is null until the run has created a new target project. startedAt: when Start or Re-run was pressed. */
export interface ImportJobDto {
  id: string;
  jiraProjectKey: string;
  projectKey: string;
  projectId: string | null;
  status: ImportJobState;
  progress: ImportJobProgressJson;
  startedAt: string | null;
  startedLabel: string | null;
  finishedAt: string | null;
  finishedLabel: string | null;
  createdBy: UserRef | null;
}

export interface ImportErrorDto {
  id: string;
  step: ImportStep;
  entity: string;
  sourceKey: string;
  message: string;
  resolution: ImportErrorResolution;
}

export interface ImportLogLineDto {
  id: number;
  loggedAt: string;
  /** The time of day the line was logged ("14:02:05"). */
  timeLabel: string;
  level: ImportLogLevel;
  message: string;
}
