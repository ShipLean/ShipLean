// Work item shapes. C#: ShipLean.Contracts.WorkItems. Nothing is omitted on the wire: absent values are null and lists are [].
// Timestamps are ISO 8601 strings with a UTC offset, each next to a server-formatted label.
import { ApiRequestError } from './client';
import type { ChecklistItem, FieldChange, LinkType, LinkValue } from './persisted';
import type { SprintRef, StateRef, TypeRef, UserRef, WorkItemTypeId } from './refs';

export const REVISION_ORIGINS = ['user', 'jira', 'webhook'] as const;
export type RevisionOrigin = (typeof REVISION_ORIGINS)[number];

export interface ParentRefDto {
  id: string;
  key: string;
  type: TypeRef;
  title: string;
}

/** Where the card sits on its board. columnId is null until the item has been placed. */
export interface BoardPositionDto {
  columnId: string | null;
  done: boolean;
  laneId: string | null;
  columnOrder: string | null;
}

export interface OriginalSystemDto {
  system: string;
  id: string;
  url: string | null;
}

/** A work item link carries the target's id, key and title; a url link carries url and title. */
export const LINK_CARD_STATES = ['open', 'merged', 'closed'] as const;
export type LinkCardState = (typeof LINK_CARD_STATES)[number];

export const WEBHOOK_PROVIDERS = ['github', 'gitlab'] as const;
export type WebhookProvider = (typeof WEBHOOK_PROVIDERS)[number];

/** What a GitHub or GitLab webhook last reported about the pull or merge request a url link points at. */
export interface LinkCardDto {
  state: LinkCardState;
  title: string | null;
  asOf: string;
  asOfLabel: string;
  source: WebhookProvider;
}

/** Why a webhook added a link: the item's key was in the title or the source branch. */
export interface AutoLinkDto {
  via: WebhookProvider;
  matchedOn: 'title' | 'branch';
}

export interface LinkDto {
  id: string;
  type: LinkType;
  workItemId: string | null;
  key: string | null;
  title: string | null;
  url: string | null;
  card: LinkCardDto | null;
  autoLinked: AutoLinkDto | null;
}

export interface ChildSummaryDto {
  id: string;
  key: string;
  type: TypeRef;
  title: string;
  state: StateRef;
  assignee: UserRef | null;
  points: number | null;
}

export interface WorkItemDto {
  id: string;
  projectId: string;
  key: string;
  number: number;
  type: TypeRef;
  title: string;
  description: string;
  descriptionHtml: string;
  acceptanceCriteria: ChecklistItem[];
  reproSteps: string;
  reproStepsHtml: string;
  severity: number | null;
  state: StateRef;
  assignee: UserRef | null;
  createdBy: UserRef | null;
  changedBy: UserRef | null;
  sprint: SprintRef | null;
  priority: number;
  points: number | null;
  parent: ParentRefDto | null;
  tags: string[];
  position: BoardPositionDto;
  backlogOrder: string;
  links: LinkDto[];
  children: ChildSummaryDto[];
  originalSystem: OriginalSystemDto | null;
  revision: number;
  createdAt: string;
  createdAtLabel: string;
  changedAt: string;
  changedAtLabel: string;
  /** A Feature's goal and status line; "" when unset and on other types. */
  goal: string;
  statusLine: string;
  statusVerified: StatusVerifiedDto | null;
  /** A Feature's slug (set at creation, read-only after), when and its label. */
  slug: string | null;
  /** yyyy-MM-dd, a UTC calendar day. */
  startedOn: string | null;
  startedLabel: string | null;
}

/** When a Feature's status line was last written or confirmed. `stale` is the server's call: older than 14 days. `agent` and `via` are null for a change made in the app. */
export interface StatusVerifiedDto {
  at: string;
  atLabel: string;
  stale: boolean;
  by: UserRef | null;
  agent: string | null;
  via: string | null;
}

/** A field a write sets: `null` in WorkItemChanges leaves the field alone, `{ value: null }` clears a nullable field. */
export interface FieldSet<T> {
  value: T;
}

/** Every editable field is `FieldSet | null`; links are added and removed, never replaced. */
export interface WorkItemChanges {
  title: FieldSet<string> | null;
  description: FieldSet<string> | null;
  acceptanceCriteria: FieldSet<ChecklistItem[]> | null;
  reproSteps: FieldSet<string> | null;
  severity: FieldSet<number | null> | null;
  state: FieldSet<string> | null;
  assignee: FieldSet<string | null> | null;
  sprint: FieldSet<string | null> | null;
  priority: FieldSet<number> | null;
  points: FieldSet<number | null> | null;
  parent: FieldSet<string | null> | null;
  tags: FieldSet<string[]> | null;
  /** Feature only. Writing statusLine (even unchanged) or confirmStatus stamps the status as verified now. */
  goal?: FieldSet<string> | null;
  statusLine?: FieldSet<string> | null;
  confirmStatus?: boolean;
  addLinks: LinkValue[];
  removeLinkIds: string[];
}

export interface WorkItemCreate {
  projectId: string;
  type: WorkItemTypeId;
  title: string;
  parentId: string | null;
  sprintId: string | null;
  changes: WorkItemChanges | null;
  /** Feature only. The slug is set here and read-only afterwards; startedOn defaults to today (UTC). */
  goal?: string;
  statusLine?: string;
  slug?: string | null;
  startedOn?: string | null;
}

export interface WorkItemPatch {
  expectedRevision: number;
  changes: WorkItemChanges;
}

/** A card dropped on a board. beforeItemId null = last in the column; laneId null = keep the lane. columnId null = Taskboard cell: the state comes from set.state. */
export interface MoveTarget {
  expectedRevision: number;
  columnId: string | null;
  done: boolean;
  laneId: string | null;
  beforeItemId: string | null;
  set: WorkItemChanges | null;
}

/** The body of POST /api/work-items/{id}/reorder (C#: ReorderRequest). */
export interface ReorderRequest {
  expectedRevision: number;
  /** The item the moved one goes just above, or null to put it last. */
  beforeItemId: string | null;
}

export interface WriteResult {
  id: string;
  key: string;
  revision: number;
  changes: FieldChange[];
  /** Rule messages such as `pulled`. */
  notices: string[];
}

export interface RevisionDto {
  rev: number;
  changedBy: UserRef | null;
  changedAt: string;
  changedAtLabel: string;
  origin: RevisionOrigin;
  changes: FieldChange[];
  /** The agent that made the change for changedBy ("Claude Code (claude.ai)" / "Claude Code (unverified)"); null when a person did. */
  agentName: string | null;
  /** The pull or merge request a webhook change came from ("GitHub acme/api#12"); changedBy is null then. */
  webhook: string | null;
}

/** Newest first. */
export interface HistoryDto {
  revisions: RevisionDto[];
}

/** The 409 body for a stale expectedRevision: an ApiError plus the item as it is now and the revisions since the caller's, newest first. */
export interface ConflictDto {
  error: string;
  code: 'conflict';
  current: WorkItemDto;
  theirChanges: RevisionDto[];
}

/** The ConflictDto of a stale write's 409, or undefined for any other error. */
export function conflictOf(error: unknown): ConflictDto | undefined {
  if (!(error instanceof ApiRequestError) || error.status !== 409 || error.code !== 'conflict')
    return undefined;
  const body = error.body as Partial<ConflictDto> | undefined;
  return body?.current !== undefined && Array.isArray(body.theirChanges)
    ? (body as ConflictDto)
    : undefined;
}

export interface ImportedRevision {
  changedBy: string;
  changedAt: string;
  changes: FieldChange[];
}

export interface ImportedWorkItem {
  originalSystem: OriginalSystemDto;
  keyNumber: number;
  type: WorkItemTypeId;
  title: string;
  description: string;
  acceptanceCriteria: ChecklistItem[];
  reproSteps: string;
  severity: number | null;
  stateId: string;
  assigneeId: string | null;
  sprintId: string | null;
  priority: number;
  points: number | null;
  parentId: string | null;
  tags: string[];
  links: LinkValue[];
  createdBy: string;
  createdAt: string;
  changedBy: string;
  changedAt: string;
  revisions: ImportedRevision[];
}

export interface ImportResult {
  id: string;
  key: string;
  revision: number;
  created: boolean;
}
