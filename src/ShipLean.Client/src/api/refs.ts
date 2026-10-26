// Read-side pieces every DTO carries. C#: ShipLean.Contracts.Refs. Ids are lowercase UUID strings except type ids.

export const WORK_ITEM_TYPE_IDS = ['epic', 'feature', 'story', 'bug', 'task'] as const;
export type WorkItemTypeId = (typeof WORK_ITEM_TYPE_IDS)[number];

export const STATE_CATEGORIES = [
  'proposed',
  'inProgress',
  'resolved',
  'completed',
  'removed',
] as const;
export type StateCategory = (typeof STATE_CATEGORIES)[number];

export const SPRINT_STATUSES = ['past', 'current', 'future'] as const;
export type SprintStatus = (typeof SPRINT_STATUSES)[number];

export interface UserRef {
  id: string;
  name: string;
  initials: string;
  /** Lowercase #rrggbb. */
  avatarColour: string;
  photoUrl: string | null;
}

export interface StateRef {
  id: string;
  name: string;
  category: StateCategory;
}

export interface SprintRef {
  id: string;
  name: string;
  status: SprintStatus;
}

export interface TypeRef {
  id: WorkItemTypeId;
  name: string;
}
