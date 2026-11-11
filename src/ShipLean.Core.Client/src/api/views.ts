// Read-model shapes of the backlog, board and Taskboard screens. C#: ShipLean.Contracts.Views.
import type { BacklogReviewDto } from './backlogWrites';
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
import type { FeatureHealth, NeedsYouDto } from './screens';
import type { ParentRefDto } from './workItems';

export const BOARD_LANE_KINDS = ['criteria', 'manual', 'default', 'epic', 'noEpic'] as const;
export type BoardLaneKind = (typeof BOARD_LANE_KINDS)[number];

export const BACKLOG_ROW_KINDS = ['item', 'group', 'noParentGroup'] as const;
export type BacklogRowKind = (typeof BACKLOG_ROW_KINDS)[number];

export const BACKLOG_MODES = ['list', 'tree', 'planning'] as const;
export type BacklogMode = (typeof BACKLOG_MODES)[number];

/**
 * Descendants of one type. closed = Completed category. inCurrentSprint and inOtherSprint are the open ones planned into the
 * current sprint or another live sprint (both ≤ open); the rest of open is in no sprint. No parent rows send 0 and 0.
 */
export interface RollupCountDto {
  type: WorkItemTypeId;
  open: number;
  closed: number;
  inCurrentSprint: number;
  inOtherSprint: number;
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
  /** Set on Features board cards; null on other cards. */
  health: FeatureHealth | null;
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

/** A taskboard row that always has a parent, plus its section (featureId) and the story's sprint (null = Backlog). */
export interface FeatureBoardRowDto {
  parent: CardDto;
  offerResolve: boolean;
  cells: TaskboardCellDto[];
  featureId: string;
  sprint: SprintRef | null;
  /** Null when nothing waits on the caller. */
  needsYou: NeedsYouDto | null;
}

/** depth 0 is the board's Feature; parentFeature is set from depth 2. Counts cover the section's own rows; points are null when none has any. */
export interface FeatureBoardSectionDto {
  id: string;
  key: string;
  title: string;
  state: StateRef;
  depth: number;
  parentFeature: ParentRefDto | null;
  storiesClosed: number;
  storiesTotal: number;
  pointsClosed: number | null;
  pointsTotal: number | null;
}

/** The tree's roll-up, counted from the rows. sprints are ordered by start date. */
export interface FeatureBoardHeaderDto {
  id: string;
  key: string;
  title: string;
  state: StateRef;
  featuresUnder: number;
  storiesClosed: number;
  storiesTotal: number;
  pointsClosed: number | null;
  pointsTotal: number | null;
  tasksClosed: number;
  tasksTotal: number;
  sprints: SprintRef[];
  anyInBacklog: boolean;
}

/** GET of a Feature's board: rows are flat (section then row order); peopleRows cover the whole tree. */
export interface FeatureBoardViewDto {
  featureId: string;
  boardId: string;
  header: FeatureBoardHeaderDto;
  columns: TaskboardColumnDto[];
  sections: FeatureBoardSectionDto[];
  rows: FeatureBoardRowDto[];
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
  /** A Feature row's health; null elsewhere. */
  health: FeatureHealth | null;
  /** Null when nothing waits on the caller. */
  needsYou: NeedsYouDto | null;
  /** Keys of open blocked-by links; [] when none. */
  blockedBy: string[];
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
  /** The items waiting in an intake (Triage) state, which the rows leave out unless the filter names that state. */
  triageCount: number;
  /** The intake states of the backlog's types: what the triage chip filters by. */
  triageStateIds: string[];
  /** Filled for the Stories level in list and planning mode, null for tree and the other levels. */
  review: BacklogReviewDto | null;
}
