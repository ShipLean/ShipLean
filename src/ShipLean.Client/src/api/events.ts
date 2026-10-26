// The project event wire format. C#: ShipLean.Contracts.Events (ProjectEvent and its derived types). Ids only: a client
// refetches what it shows. clientId is the tab whose request caused the event (null when none); a tab ignores its own.
import type { RouteId } from '../routes';
import type { ImportJobState } from './persisted';
import type { WorkItemTypeId } from './refs';

export const PROJECT_EVENT_TYPES = [
  'workItemCreated',
  'workItemUpdated',
  'workItemMoved',
  'workItemReordered',
  'workItemRemoved',
  'boardSettingsChanged',
  'sprintChanged',
  'statesChanged',
  'projectChanged',
  'presenceChanged',
  'importProgress',
  'commentsChanged',
  'attachmentsChanged',
  'linksChanged',
] as const;
export type ProjectEventType = (typeof PROJECT_EVENT_TYPES)[number];

interface ProjectEventBase<T extends ProjectEventType> {
  type: T;
  projectId: string;
  clientId: string | null;
}

export interface WorkItemCreated extends ProjectEventBase<'workItemCreated'> {
  id: string;
  key: string;
  workItemType: WorkItemTypeId;
  /** The parent whose roll-ups change, or null. */
  parentId: string | null;
}

export interface WorkItemUpdated extends ProjectEventBase<'workItemUpdated'> {
  id: string;
  revision: number;
  /** Ids of the fields that changed. */
  fields: string[];
  /** A work item rule amended the change with a "pulled" notice. */
  pulled: boolean;
}

export interface WorkItemMoved extends ProjectEventBase<'workItemMoved'> {
  id: string;
  revision: number;
  // null for a Taskboard drop
  columnId: string | null;
  done: boolean;
  laneId: string | null;
  // null when the card left its board
  columnOrder: string | null;
  stateId: string;
  parentId: string | null;
}

export interface WorkItemReordered extends ProjectEventBase<'workItemReordered'> {
  id: string;
  backlogOrder: string;
}

export interface WorkItemRemoved extends ProjectEventBase<'workItemRemoved'> {
  id: string;
  /** The parent whose roll-ups change, or null. */
  parentId: string | null;
}

export interface BoardSettingsChanged extends ProjectEventBase<'boardSettingsChanged'> {
  boardId: string;
  /** Top-level property names of the board settings document that changed. */
  sections: string[];
}

export interface SprintChanged extends ProjectEventBase<'sprintChanged'> {
  sprintId: string;
}

export type StatesChanged = ProjectEventBase<'statesChanged'>;

export type ProjectChanged = ProjectEventBase<'projectChanged'>;

export interface PresenceChanged extends ProjectEventBase<'presenceChanged'> {
  screenId: RouteId;
  userIds: string[];
  /** Grows with every presence change: a list with a lower sequence than the last one for its screen is older. */
  sequence: number;
}

export interface ImportProgress extends ProjectEventBase<'importProgress'> {
  jobId: string;
  percent: number;
  state: ImportJobState;
  message: string;
}

export interface CommentsChanged extends ProjectEventBase<'commentsChanged'> {
  workItemId: string;
}

export interface AttachmentsChanged extends ProjectEventBase<'attachmentsChanged'> {
  workItemId: string;
}

export interface LinksChanged extends ProjectEventBase<'linksChanged'> {
  workItemId: string;
}

export type ProjectEvent =
  | WorkItemCreated
  | WorkItemUpdated
  | WorkItemMoved
  | WorkItemReordered
  | WorkItemRemoved
  | BoardSettingsChanged
  | SprintChanged
  | StatesChanged
  | ProjectChanged
  | PresenceChanged
  | ImportProgress
  | CommentsChanged
  | AttachmentsChanged
  | LinksChanged;
