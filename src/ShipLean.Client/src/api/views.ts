// Read-model shapes of the backlog, board and Taskboard screens. C#: ShipLean.Contracts.Views.
import type { BoardLevel, BoardTypeStatesDto } from './boards';
import type {
  CardAnnotationsJson,
  CardFieldSettingJson,
  StyleRuleJson,
  Swatch,
  TagColourJson,
} from './persisted';
import type { ForecastDto, VelocityDto } from './sprints';
import type { SprintRef, StateRef, TypeRef, UserRef, WorkItemTypeId } from './refs';
import type { ParentRefDto } from './workItems';

export const BOARD_LANE_KINDS = ['criteria', 'manual', 'default', 'epic', 'noEpic'] as const;
export type BoardLaneKind = (typeof BOARD_LANE_KINDS)[number];

export const BACKLOG_ROW_KINDS = ['item', 'group', 'noParentGroup'] as const;
export type BacklogRowKind = (typeof BACKLOG_ROW_KINDS)[number];

export const BACKLOG_MODES = ['list', 'tree', 'planning'] as const;
export type BacklogMode = (typeof BACKLOG_MODES)[number];

/** Descendants of one type. closed = Completed category. */
export interface RollupCountDto {
  type: WorkItemTypeId;
  open: number;
  closed: number;
}

/** colour is null for a tag the board gives no colour. */
export interface CardTagDto {
  name: string;
  colour: Swatch | null;
}

/** An extra field the settings ask a card to show; text is null when empty. */
export interface CardFieldDto {
  fieldId: string;
  label: string;
  text: string | null;
}

/** assignee is the Owner on the Features and Epics boards. sumWarn: childPoints exceeds points by more than 25 %. */
export interface CardDto {
  id: string;
  key: string;
  type: TypeRef;
  title: string;
  state: StateRef;
  assignee: UserRef | null;
  points: number | null;
  childPoints: number | null;
  sumWarn: boolean;
  parent: ParentRefDto | null;
  tags: CardTagDto[];
  rollups: RollupCountDto[];
  offerDone: boolean;
  styleRuleId: string | null;
  extraFields: CardFieldDto[];
  revision: number;
}

export interface ColumnStateDto {
  type: WorkItemTypeId;
  state: StateRef;
}

export interface BoardColumnDto {
  id: string;
  name: string;
  wipLimit: number | null;
  count: number;
  /** Cards the filter hides; they still count toward WIP. */
  hiddenCount: number;
  overLimit: boolean;
  split: boolean;
  doingCount: number;
  doneCount: number;
  definitionOfDone: string;
  definitionOfDoneHtml: string;
  states: ColumnStateDto[];
}

/** olderCursor is null when olderCount is 0. */
export interface BoardCellDto {
  columnId: string;
  doneHalf: boolean;
  cards: CardDto[];
  olderCount: number;
  olderCursor: string | null;
}

/** epic is set on a generated Epic lane; acceptsDrops is false when the lane's criteria are not all "=" clauses. */
export interface BoardLaneDto {
  id: string;
  name: string;
  colour: Swatch | null;
  kind: BoardLaneKind;
  criteriaText: string | null;
  epic: ParentRefDto | null;
  acceptsDrops: boolean;
  cardCount: number;
  openPoints: number;
  cells: BoardCellDto[];
}

/** Filters are query parameters, applied on the server before Done is truncated. */
export interface BoardViewDto {
  boardId: string;
  projectId: string;
  level: BoardLevel;
  openCount: number;
  openPoints: number;
  columns: BoardColumnDto[];
  lanes: BoardLaneDto[];
  styleRules: StyleRuleJson[];
  cardFields: CardFieldSettingJson[];
  annotations: CardAnnotationsJson;
}

export interface TaskboardColumnDto {
  state: StateRef;
  count: number;
}

export interface TaskboardCellDto {
  stateId: string;
  tasks: CardDto[];
}

/** parent is null for the unparented row, which is last. */
export interface TaskboardRowDto {
  parent: CardDto | null;
  offerResolve: boolean;
  cells: TaskboardCellDto[];
}

/** person is null for the Unassigned row, which is first. */
export interface TaskboardPersonRowDto {
  person: UserRef | null;
  taskCount: number;
  cells: TaskboardCellDto[];
}

export interface TaskboardViewDto {
  sprintId: string;
  boardId: string;
  columns: TaskboardColumnDto[];
  rows: TaskboardRowDto[];
  peopleRows: TaskboardPersonRowDto[];
  parentStates: BoardTypeStatesDto[];
  styleRules: StyleRuleJson[];
  cardFields: CardFieldSettingJson[];
  annotations: CardAnnotationsJson;
}

/** type and state are null on the noParentGroup row. Group rollups and childPoints count the filtered children. */
export interface BacklogRowDto {
  kind: BacklogRowKind;
  id: string;
  key: string;
  type: TypeRef | null;
  title: string;
  state: StateRef | null;
  assignee: UserRef | null;
  points: number | null;
  childPoints: number | null;
  sumWarn: boolean;
  sprint: SprintRef | null;
  parent: ParentRefDto | null;
  depth: number;
  rank: number | null;
  tags: CardTagDto[];
  rollups: RollupCountDto[];
  revision: number;
}

/** sprint is null for the Backlog section; capacity is null when none is entered. */
export interface BacklogSectionDto {
  id: string;
  title: string;
  sprint: SprintRef | null;
  points: number;
  capacity: number | null;
  rows: BacklogRowDto[];
}

/** List and tree fill rows; planning fills sections. forecast and velocity are null unless asked for. */
export interface BacklogViewDto {
  projectId: string;
  level: BoardLevel;
  mode: BacklogMode;
  totalCount: number;
  rows: BacklogRowDto[];
  sections: BacklogSectionDto[];
  tagColours: TagColourJson[];
  forecast: ForecastDto | null;
  velocity: VelocityDto | null;
}
