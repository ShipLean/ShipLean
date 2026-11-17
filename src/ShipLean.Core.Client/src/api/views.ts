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
import type { SprintRef, StateCategory, StateRef, TypeRef, UserRef, WorkItemTypeId } from './refs';
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

// Feature timeline (C1, SHIP-1465). C#: ShipLean.Contracts.Views.Timeline. Every instant is an opaque ISO string; positions are
// hour offsets from scale.start and labels come from the server, so the browser does no date maths.

export const KEY_DATE_POINTS = ['added', 'planned', 'executionStarted', 'finished'] as const;
export type KeyDatePoint = (typeof KEY_DATE_POINTS)[number];

export const TIMELINE_BAR_STATES = ['inFlight', 'resolved', 'closed'] as const;
export type TimelineBarState = (typeof TIMELINE_BAR_STATES)[number];

export const TIMELINE_PERIOD_STATES = [
  'new',
  'active',
  'blocked',
  'resolved',
  'closed',
  'removed',
] as const;
export type TimelinePeriodState = (typeof TIMELINE_PERIOD_STATES)[number];

export const REVIEW_VERDICTS = ['approved', 'changesRequested'] as const;
export type ReviewVerdict = (typeof REVIEW_VERDICTS)[number];

export const TIMELINE_EDGE_KINDS = ['depends', 'blocked'] as const;
export type TimelineEdgeKind = (typeof TIMELINE_EDGE_KINDS)[number];

export const TIMELINE_EDGE_SOURCES = ['link', 'wave'] as const;
export type TimelineEdgeSource = (typeof TIMELINE_EDGE_SOURCES)[number];

/** An agent (agent true, userId null, name = the agent's name) or a person. */
export interface TimelinePersonDto {
  userId: string | null;
  name: string;
  agent: boolean;
}

export interface TimelineEpicDto {
  key: string;
  title: string;
}

export interface TimelineFeatureDto {
  id: string;
  key: string;
  title: string;
  state: string;
  stateCategory: StateCategory;
  epic: TimelineEpicDto | null;
}

/** at, dayLabel, by and source are null until the point is reached. */
export interface TimelineKeyDateDto {
  point: KeyDatePoint;
  at: string | null;
  dayLabel: string | null;
  by: TimelinePersonDto | null;
  source: string | null;
}

/** days and label are null while from isn't reached; soFar when from is reached and to isn't (runs to asOf). */
export interface TimelineGapDto {
  from: KeyDatePoint;
  to: KeyDatePoint;
  days: number | null;
  label: string | null;
  soFar: boolean;
}

export interface TimelineExecutionDto {
  start: string | null;
  end: string | null;
  calendarDays: number | null;
}

/** One calendar day in the viewer's zone; a daylight-saving day is 23 or 25 hours long. */
export interface TimelineDayDto {
  startHours: number;
  label: string;
  dayOfMonth: string;
  weekend: boolean;
  monthStart: boolean;
}

export interface TimelineSprintDto {
  name: string;
  startHours: number;
  endHours: number;
}

/** start is the UTC instant of the midnight that starts the first day drawn; nowHours is null once finished. */
export interface TimelineScaleDto {
  start: string;
  endHours: number;
  nowHours: number | null;
  days: TimelineDayDto[];
  sprints: TimelineSprintDto[];
}

/** A story's or bug's bracket: first task Active to last task Resolved; end is null while a task is open. */
export interface TimelineStoryDto {
  key: string;
  type: Extract<WorkItemTypeId, 'story' | 'bug'>;
  title: string;
  assignee: TimelinePersonDto | null;
  start: string | null;
  end: string | null;
  startHours: number | null;
  endHours: number | null;
  hours: number | null;
  startLabel: string | null;
  endLabel: string | null;
  durationLabel: string | null;
  tasks: string[];
}

/** stateName is the workflow's name (a blocked period is still in Active); the period a task ends in has to null. */
export interface TimelinePeriodDto {
  state: TimelinePeriodState;
  stateName: string;
  from: string;
  to: string | null;
  fromLabel: string;
  hours: number | null;
  durationLabel: string | null;
}

export interface TimelineReviewDto {
  at: string;
  atHours: number;
  atLabel: string;
  verdict: ReviewVerdict;
  by: TimelinePersonDto;
  note: string | null;
}

/** One task's bar. inferred = closed with no start recorded: start is null, drawn as a diamond at end. */
export interface TimelineBarDto {
  key: string;
  type: Extract<WorkItemTypeId, 'task'>;
  title: string;
  parent: string;
  assignee: TimelinePersonDto | null;
  state: TimelineBarState;
  start: string | null;
  end: string | null;
  startHours: number | null;
  endHours: number | null;
  hours: number | null;
  startLabel: string | null;
  endLabel: string | null;
  durationLabel: string | null;
  inferred: boolean;
  onCriticalPath: boolean;
  history: TimelinePeriodDto[];
  reviews: TimelineReviewDto[];
  /** The last commit with a recorded time: its instant, hour offset from scale.start and label. All null when none is known. */
  lastCommitAt: string | null;
  lastCommitHours: number | null;
  lastCommitLabel: string | null;
}

/** waitLabel only when waitHours ≥ 12; unblocked* belong to blocked edges and are null while still blocked. */
export interface TimelineEdgeDto {
  from: string;
  to: string;
  kind: TimelineEdgeKind;
  source: TimelineEdgeSource;
  waitHours: number | null;
  waitLabel: string | null;
  unblockedAt: string | null;
  unblockedHours: number | null;
  onCriticalPath: boolean;
}

/** parent is null for a story or bug. */
export interface TimelineNotStartedDto {
  key: string;
  type: WorkItemTypeId;
  title: string;
  parent: string | null;
}

export interface TimelineLinksDto {
  ui: string;
}

/** GET of a Feature's timeline; the MCP tool feature_timeline sends the same fields in snake_case. */
export interface FeatureTimelineViewDto {
  feature: TimelineFeatureDto;
  asOf: string;
  finished: boolean;
  keyDates: TimelineKeyDateDto[];
  gaps: TimelineGapDto[];
  execution: TimelineExecutionDto;
  scale: TimelineScaleDto;
  /** Empty unless finished. */
  criticalPath: string[];
  stories: TimelineStoryDto[];
  bars: TimelineBarDto[];
  edges: TimelineEdgeDto[];
  notStarted: TimelineNotStartedDto[];
  links: TimelineLinksDto;
}

// Inspection's lead time (SHIP-1398 K2): InspectionMetricsViewDto and its parts, pinned by inspection-metrics-view.json.

export const INSPECTION_RANGES = ['last6', 'quarter', 'lastQuarter', 'all'] as const;
export type InspectionRange = (typeof INSPECTION_RANGES)[number];

export const INSPECTION_CUTS = ['none', 'feature', 'who'] as const;
export type InspectionCut = (typeof INSPECTION_CUTS)[number];

export const INSPECTION_CADENCES = ['sprints', 'weeks'] as const;
export type InspectionCadence = (typeof INSPECTION_CADENCES)[number];

export const INSPECTION_AGGREGATES = ['mean'] as const;
export type InspectionAggregate = (typeof INSPECTION_AGGREGATES)[number];

export const LEAD_TIME_ENDED_BY = ['lastCommit', 'resolved'] as const;
export type LeadTimeEndedBy = (typeof LEAD_TIME_ENDED_BY)[number];

export const INSPECTION_PARENT_TYPES = ['story', 'bug'] as const;
export type InspectionParentType = (typeof INSPECTION_PARENT_TYPES)[number];

export const INSPECTION_SEGMENT_KINDS = ['active', 'blocked', 'rework', 'unresolvedTail'] as const;
export type InspectionSegmentKind = (typeof INSPECTION_SEGMENT_KINDS)[number];

export const INSPECTION_EVENT_KINDS = [
  'started',
  'commit',
  'resolved',
  'blocked',
  'unblocked',
  'reviewApproved',
  'reviewChangesRequested',
] as const;
export type InspectionEventKind = (typeof INSPECTION_EVENT_KINDS)[number];

export interface InspectionProjectDto {
  id: string;
  key: string;
  name: string;
}

/** Tasks of the range that ended at a commit time; percent is null with no tasks. Below 70% the page shows a banner. */
export interface InspectionCoverageDto {
  tasks: number;
  withCommitTime: number;
  percent: number | null;
}

/** One group of tasks' lead time: means in hours to one decimal (null with no tasks), each with its server label. */
export interface LeadTimeStatsDto {
  tasks: number;
  meanHours: number | null;
  meanLabel: string | null;
  meanActiveHours: number | null;
  meanActiveLabel: string | null;
  meanBlockedHours: number | null;
  meanBlockedLabel: string | null;
  meanReworkHours: number | null;
  meanReworkLabel: string | null;
  meanUnresolvedTailHours: number | null;
  meanUnresolvedTailLabel: string | null;
  endedAtResolved: number;
  changePctVsPrevious: number | null;
}

/** Escaping bugs split by severity (sev1 critical to sev4 low, unset without one); they sum to escaping. */
export interface InspectionSeverityCountsDto {
  sev1: number;
  sev2: number;
  sev3: number;
  sev4: number;
  unset: number;
}

/**
 * Escaping bugs over a period, quarter or the range (SHIP-1398 QK3): every Bug not found before release, in the sprint it was
 * filed (or its cause's sprint once it has a Caused by link). mergedPrs and per100Prs are null when no linked PR merged.
 * changePctVsPrevious is the whole-percent change in escaping against the sprint before, for a single sprint only; null for a
 * quarter, the range, the first sprint and when the sprint before had none.
 */
export interface InspectionQualityStatsDto {
  escaping: number;
  weighted: number;
  foundBeforeRelease: number;
  mergedPrs: number | null;
  per100Prs: number | null;
  escapingBySeverity: InspectionSeverityCountsDto;
  changePctVsPrevious: number | null;
}

/**
 * A bug counted in a shown sprint; severity 1 (critical) to 4 (low) or null; causedBy* name the Task that introduced it;
 * periodId is the sprint it counts in (filed, or its cause's sprint).
 */
export interface InspectionBugDto {
  key: string;
  title: string;
  severity: number | null;
  filedAt: string;
  filedLabel: string;
  foundBeforeRelease: boolean;
  causedByKey: string | null;
  causedByTitle: string | null;
  periodId: string;
}

export interface InspectionHeadlinePeriodDto {
  periodId: string;
  name: string;
  leadTime: LeadTimeStatsDto;
  /** Null exactly when the project has no live bug at all; so is every other quality and bugs in the view. */
  quality: InspectionQualityStatsDto | null;
}

/** The last finished sprint, the running one so far, this quarter to date and last quarter; each null when there is none. */
export interface InspectionHeadlineDto {
  finished: InspectionHeadlinePeriodDto | null;
  soFar: InspectionHeadlinePeriodDto | null;
  quarterToDate: InspectionQuarterDto | null;
  lastQuarter: InspectionQuarterDto | null;
}

export interface InspectionDotDto {
  taskKey: string;
  title: string;
  leadHours: number;
  leadLabel: string;
  endedBy: LeadTimeEndedBy;
  overTwoDays: boolean;
  longest: boolean;
}

export interface InspectionPeriodDto {
  id: string;
  name: string;
  shortName: string;
  startDate: string;
  endDate: string;
  datesLabel: string;
  quarter: string;
  inProgress: boolean;
  leadTime: LeadTimeStatsDto;
  dots: InspectionDotDto[];
  quality: InspectionQualityStatsDto | null;
}

export interface InspectionQuarterDto {
  quarter: string;
  toDate: boolean;
  periodIds: string[];
  leadTime: LeadTimeStatsDto;
  quality: InspectionQualityStatsDto | null;
}

/** One row of the cut table; cells has one entry per period, in periods order. */
export interface InspectionCutRowDto {
  key: string;
  label: string;
  agent: boolean;
  cells: LeadTimeStatsDto[];
  all: LeadTimeStatsDto;
}

export interface InspectionTaskDto {
  key: string;
  title: string;
  parentKey: string | null;
  parentType: InspectionParentType | null;
  featureKey: string | null;
  featureTitle: string | null;
  who: string;
  agent: boolean;
  startedAt: string;
  startedLabel: string;
  endedAt: string;
  endedLabel: string;
  endedBy: LeadTimeEndedBy;
  leadHours: number;
  leadLabel: string;
  activeHours: number;
  blockedHours: number;
  reworkHours: number;
  unresolvedTailHours: number;
  reviews: number;
  commits: number;
  periodId: string | null;
}

export interface InspectionLeftOutDto {
  key: string;
  title: string;
  featureKey: string | null;
  reason: string;
  at: string;
  atLabel: string;
}

export interface InspectionPeriodFeatureDto {
  key: string;
  title: string;
  tasksEnded: number;
  tasksEndedBefore: number;
  tasksTotal: number;
  leadTime: LeadTimeStatsDto;
  longestTaskKey: string | null;
  longestLeadLabel: string | null;
  executionLabel: string | null;
}

/** A neighbouring sprint, for the period header's previous and next buttons. */
export interface InspectionPeriodRefDto {
  id: string;
  shortName: string;
}

export interface InspectionSegmentDto {
  kind: InspectionSegmentKind;
  from: string;
  to: string;
  fromHours: number;
  hours: number;
  label: string;
  counted: boolean;
}

/** An event with at > the task's endedAt happened after the lead time ended: the rail styles it as not counted. */
export interface InspectionEventDto {
  kind: InspectionEventKind;
  at: string;
  atLabel: string;
  hours: number;
  text: string;
}

export interface InspectionTaskMeasurementDto {
  task: InspectionTaskDto;
  segments: InspectionSegmentDto[];
  events: InspectionEventDto[];
}

export interface InspectionPeriodDetailDto {
  periodId: string;
  name: string;
  datesLabel: string;
  quarter: string;
  /** Neighbours over all started sprints, not only the range; null at either end. */
  previous: InspectionPeriodRefDto | null;
  next: InspectionPeriodRefDto | null;
  inProgress: boolean;
  leadTime: LeadTimeStatsDto;
  tasks: InspectionTaskDto[];
  features: InspectionPeriodFeatureDto[];
  /** Null unless select names one of this sprint's tasks; an unknown key selects nothing. */
  selected: InspectionTaskMeasurementDto | null;
  quality: InspectionQualityStatsDto | null;
  /** Every bug counted in this sprint, found before release ones included, oldest filed first. */
  bugs: InspectionBugDto[] | null;
}

/**
 * Tokens over a period, a quarter or the range (SHIP-1398 CK2). tasksWithRuns counts the tasks with at least one counted run;
 * tokensPerTask is the mean over those only. tokensPerMergedPr is null when no PR merged. changePctVsPrevious is the
 * whole-percent change in tokensPerTask against the sprint before, for a single sprint only. kept is false when the span
 * started before the store kept runs: its numbers are then null and its labels read "—".
 */
export interface InspectionCostStatsDto {
  tasks: number;
  tasksWithRuns: number;
  tokensPerTask: number | null;
  tokensPerTaskLabel: string;
  tokensTotal: number | null;
  tokensTotalLabel: string;
  mergedPrs: number | null;
  tokensPerMergedPr: number | null;
  tokensPerMergedPrLabel: string;
  changePctVsPrevious: number | null;
  kept: boolean;
  tasksWithRunsPct: number | null;
}

export interface InspectionPeriodCostDto {
  periodId: string;
  stats: InspectionCostStatsDto;
}

export interface InspectionQuarterCostDto {
  quarter: string;
  stats: InspectionCostStatsDto;
}

/** As the lead-time headline: the last finished sprint, the running one so far, this quarter to date and last quarter. */
export interface InspectionCostHeadlineDto {
  finished: InspectionCostStatsDto | null;
  soFar: InspectionCostStatsDto | null;
  quarterToDate: InspectionCostStatsDto | null;
  lastQuarter: InspectionCostStatsDto | null;
}

/** One of the range's tasks with the most tokens; agent says whether who is an agent; periodId is where its lead time ended. */
export interface InspectionCostTaskDto {
  key: string;
  title: string;
  featureKey: string | null;
  who: string;
  agent: boolean;
  models: string[];
  runs: number;
  tokens: number;
  tokensLabel: string;
  leadLabel: string;
  periodId: string | null;
}

/** The range's tokens of one Feature; key null for tasks with no Feature. */
export interface InspectionCostFeatureDto {
  key: string | null;
  title: string;
  tasks: number;
  tokens: number;
  tokensLabel: string;
}

/**
 * Tokens per task from the agent runs (SHIP-1398 CK2): tokens only, never money. available is false, with a note, when the
 * store cannot total tokens per task; its lists are then empty and range null. mostTokens is the range's top 8, most first.
 */
export interface InspectionCostDto {
  available: boolean;
  note: string | null;
  periods: InspectionPeriodCostDto[];
  quarters: InspectionQuarterCostDto[];
  range: InspectionCostStatsDto | null;
  headline: InspectionCostHeadlineDto;
  mostTokens: InspectionCostTaskDto[];
  byFeature: InspectionCostFeatureDto[];
}

/** GET of a project's Inspection lead time; the MCP tool inspection_metrics sends the same fields in snake_case. */
export interface InspectionMetricsViewDto {
  project: InspectionProjectDto;
  cadence: InspectionCadence;
  range: InspectionRange;
  cut: InspectionCut;
  aggregate: InspectionAggregate;
  generatedAt: string;
  caveat: string;
  coverage: InspectionCoverageDto;
  headline: InspectionHeadlineDto;
  periods: InspectionPeriodDto[];
  quarters: InspectionQuarterDto[];
  /** Null when cut is none. */
  cuts: InspectionCutRowDto[] | null;
  longest: InspectionTaskDto[];
  leftOut: InspectionLeftOutDto[];
  /** Null unless the query names a period. */
  periodDetail: InspectionPeriodDetailDto | null;
  /** Escaping bugs over the shown sprints; null exactly when the project has no live bug at all. */
  quality: InspectionQualityStatsDto | null;
  /** Every bug counted in the shown sprints, found before release ones included, oldest filed first; null as quality. */
  bugs: InspectionBugDto[] | null;
  /** Tokens per task from the agent runs; null when agent runs are off for the instance or the project. */
  cost: InspectionCostDto | null;
}
