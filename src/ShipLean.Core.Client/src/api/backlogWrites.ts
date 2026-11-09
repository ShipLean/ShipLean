// Wire shapes of the Stories backlog writes and the review checks. C#: ShipLean.Contracts.Backlog.
import type { WorkItemTypeId } from './refs';
import type { ConflictDto, WorkItemChanges, WriteResult } from './workItems';

/** The most rows one bulk action touches, everywhere (C# BulkLimits.MaxRows). */
export const BULK_MAX_ROWS = 100;

export const BULK_SLOTS = ['first', 'last', 'before'] as const;
export type BulkSlot = (typeof BULK_SLOTS)[number];

/** beforeItemId only with slot 'before'. */
export interface BulkPlacement {
  slot: BulkSlot;
  beforeItemId: string | null;
}

/** changes null with a placement is position only (no revision check); both null is a 400. */
export interface BulkWriteItem {
  id: string;
  expectedRevision: number;
  changes: WorkItemChanges | null;
  placement: BulkPlacement | null;
}

/** POST api/projects/{id}/backlog/bulk: 1 to BULK_MAX_ROWS items with unique ids. */
export interface BulkWriteRequest {
  items: BulkWriteItem[];
}

/** A stale item is not an error: it is in stale, the others are written. Both are in request order. */
export interface BulkWriteResult {
  written: WriteResult[];
  stale: ConflictDto[];
}

export interface BacklogAddTask {
  title: string;
  points: number | null;
}

export interface BacklogAddItem {
  type: WorkItemTypeId;
  title: string;
  description: string | null;
  points: number | null;
  parentId: string | null;
  tasks: BacklogAddTask[];
}

/** POST api/projects/{id}/backlog/items: 1 to BULK_MAX_ROWS rows counting tasks; a null placement means first. */
export interface BacklogAddRequest {
  items: BacklogAddItem[];
  sprintId: string | null;
  placement: BulkPlacement | null;
}

/** Each item, then its tasks, in input order. */
export interface BacklogAddResult {
  created: WriteResult[];
}

/** POST api/projects/{id}/backlog/duplicates: 1 to BULK_MAX_ROWS titles. */
export interface DuplicateCheckRequest {
  titles: string[];
}

export interface DuplicateItem {
  key: string;
  title: string;
}

/** index is the title's place in the request. */
export interface DuplicateMatch {
  index: number;
  items: DuplicateItem[];
}

/** Only titles with a match. */
export interface DuplicateCheckResult {
  matches: DuplicateMatch[];
}

export const BACKLOG_CHECK_IDS = [
  'unestimated',
  'no_acceptance_criteria',
  'no_parent',
  'stale',
  'likely_duplicate',
] as const;
export type BacklogCheckId = (typeof BACKLOG_CHECK_IDS)[number];

export interface BacklogReviewCheckDto {
  id: BacklogCheckId;
  itemIds: string[];
}

/** The five checks over the top of the unfiltered stack, always all five in BACKLOG_CHECK_IDS order. */
export interface BacklogReviewDto {
  top: number;
  staleDays: number;
  checks: BacklogReviewCheckDto[];
}
