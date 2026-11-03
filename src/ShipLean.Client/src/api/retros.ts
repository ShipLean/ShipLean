// Retrospective shapes (SHIP-82 contract C3). C# twin: ShipLean.Contracts.Retros. Fetchers live in retrosApi.ts.
import type { SprintRef, StateRef, TypeRef, UserRef } from './refs';

export const RETRO_CATEGORIES = ['wentWell', 'notInPlan', 'wentBadly', 'improve'] as const;
export type RetroCategory = (typeof RETRO_CATEGORIES)[number];

export const RETRO_ROLES = ['conductor', 'subAgent', 'person'] as const;
export type RetroRole = (typeof RETRO_ROLES)[number];

export const RETRO_THEME_SET_BY = ['auto', 'agent', 'person'] as const;
export type RetroThemeSetBy = (typeof RETRO_THEME_SET_BY)[number];

export const RETRO_TONES = ['good', 'bad'] as const;
export type RetroTone = (typeof RETRO_TONES)[number];

export const RETRO_THEME_MODES = ['auto', 'pick'] as const;
export type RetroThemeMode = (typeof RETRO_THEME_MODES)[number];

export const RETRO_THEME_STATUSES = ['recurring', 'beingAddressed', 'resolved', 'quiet'] as const;
export type RetroThemeStatus = (typeof RETRO_THEME_STATUSES)[number];

export const RETRO_VERDICTS = ['helped', 'someEffect', 'noEffect', 'tooEarly'] as const;
export type RetroVerdict = (typeof RETRO_VERDICTS)[number];

export const RETRO_COVERAGES = ['done', 'missing', 'waiting'] as const;
export type RetroCoverage = (typeof RETRO_COVERAGES)[number];

export const RETRO_FAILURE_KINDS = ['blocker', 'assumptionDisproved', 'friction'] as const;
export type RetroFailureKind = (typeof RETRO_FAILURE_KINDS)[number];

export const RETRO_DECISIONS = ['undecided', 'actioned', 'notNeeded'] as const;
export type RetroDecision = (typeof RETRO_DECISIONS)[number];

export const RETRO_FIX_TYPES = ['skill', 'guide', 'test', 'analyzer', 'product', 'other'] as const;
export type RetroFixType = (typeof RETRO_FIX_TYPES)[number];

export const RETRO_INSIGHTS_MODES = ['sprint', 'week'] as const;
export type RetroInsightsMode = (typeof RETRO_INSIGHTS_MODES)[number];

export const RETRO_INSIGHT_COLUMN_KINDS = ['sprint', 'week'] as const;
export type RetroInsightColumnKind = (typeof RETRO_INSIGHT_COLUMN_KINDS)[number];

export interface RetroThemeRefDto {
  id: string;
  name: string;
  tone: RetroTone;
}

export interface RetroItemRefDto {
  id: string;
  key: string;
  type: TypeRef;
  title: string;
}

export interface RetroAuthorDto {
  user: UserRef;
  agentName: string | null;
  role: RetroRole;
}

export interface RetroActionRefDto {
  workItemId: string;
  key: string;
  title: string;
  state: StateRef;
  sprint: SprintRef | null;
  points: number | null;
  /** Null for a legacy action item that was back-copied without one. */
  fixType: RetroFixType | null;
  /** Live linked points. */
  pointCount: number;
}

export interface RetroNotNeededDto {
  reason: string;
  by: RetroAuthorDto;
  decidedAtLabel: string;
}

export interface RetroPointDto {
  id: string;
  retroId: string;
  category: RetroCategory;
  text: string;
  rootCause: string | null;
  theme: RetroThemeRefDto | null;
  themeSetBy: RetroThemeSetBy | null;
  author: RetroAuthorDto;
  workItem: RetroItemRefDto | null;
  sprint: SprintRef | null;
  isFeature: boolean;
  recurring: boolean;
  /** The live link only. */
  action: RetroActionRefDto | null;
  canArchive: boolean;
  /** Null for a Went well point. */
  decision: RetroDecision | null;
  notNeeded: RetroNotNeededDto | null;
}

/** Position order within each list. */
export interface RetroFailureLogDto {
  blockers: string[];
  assumptionsDisproved: string[];
  friction: string[];
}

export interface RetroCardDto {
  id: string;
  workItem: RetroItemRefDto | null;
  sprint: SprintRef | null;
  author: RetroAuthorDto;
  partial: boolean;
  /** ISO 8601 with offset, as the API writes it (+00:00). */
  createdAt: string;
  /** Built on the server with DisplayDate ("2h ago", "Yesterday", "28 Oct"). */
  createdAtLabel: string;
  compiledFrom: RetroItemRefDto[];
  points: RetroPointDto[];
  failureLog: RetroFailureLogDto;
}

export interface RetroActionRowDto {
  action: RetroActionRefDto;
  theme: RetroThemeRefDto | null;
  raisedIn: SprintRef | null;
  owner: UserRef | null;
  impact: RetroVerdict | null;
}

export interface RetroMentionStripDto {
  theme: RetroThemeRefDto;
  /** Aligned with the view's `sprints`. */
  counts: number[];
  /** Index into `sprints` where a fix closed. */
  fixAt: number | null;
}

export interface RetroKpisDto {
  withRetro: number;
  finished: number;
  recurringThemes: number;
  actionItemsRaised: number;
  pointsToDecide: number;
}

export interface RetroThemeGroupDto {
  /** Null for the Unthemed group, which comes last. */
  theme: RetroThemeRefDto | null;
  recurring: boolean;
  points: RetroPointDto[];
}

export interface RetroCategoryColumnDto {
  category: RetroCategory;
  groups: RetroThemeGroupDto[];
  /** Undecided points in the column, regardless of any filter. */
  toDecide: number;
}

export interface RetroCoverageRowDto {
  item: RetroItemRefDto;
  status: RetroCoverage;
}

export interface SprintRetroDto {
  sprint: SprintRef;
  kpis: RetroKpisDto;
  sprints: SprintRef[];
  /** Columns run in RETRO_CATEGORIES order. */
  categories: RetroCategoryColumnDto[];
  cards: RetroCardDto[];
  coverage: RetroCoverageRowDto[];
  actionItems: RetroActionRowDto[];
  recurring: RetroMentionStripDto[];
}

export interface RetroSearchDto {
  rows: RetroPointDto[];
  /** Ids of returned rows that matched only through their retro's raw failure log. */
  logMatchPointIds: string[];
  total: number;
  sprints: SprintRef[];
  themes: RetroThemeRefDto[];
  selected: RetroCardDto | null;
}

export interface RetroThemeInsightDto {
  theme: RetroThemeRefDto;
  counts: number[];
  total: number;
  latest: string | null;
  status: RetroThemeStatus;
  actionItems: RetroActionRefDto[];
  lastVerdict: RetroVerdict | null;
  /** Mentions with no sprint; 0 in week mode. */
  noSprint: number;
}

export interface RetroImpactDto {
  action: RetroActionRefDto;
  theme: RetroThemeRefDto;
  verdict: RetroVerdict;
  counts: number[];
  closedAt: number;
  beforeAvg: number;
  /** Null when the verdict is tooEarly (no sprint has ended since the action closed). */
  afterAvg: number | null;
  /** A fraction, (after - before) / before, e.g. -0.5 for half as many mentions; not a percentage. */
  changePct: number | null;
  latestMention: RetroPointDto | null;
}

export interface RetroInsightColumnDto {
  /** The sprint id, or the week's Monday as yyyy-MM-dd. */
  key: string;
  label: string;
  kind: RetroInsightColumnKind;
  isCurrent: boolean;
}

export interface RetroUnthemedDto {
  counts: number[];
  noSprint: number;
  total: number;
}

export interface RetroMergeProposalDto {
  id: string;
  from: RetroThemeRefDto;
  into: RetroThemeRefDto;
  reason: string;
  pointCount: number;
  proposedBy: RetroAuthorDto;
}

export interface RetroInsightsDto {
  /** The requested range: '6', '3', 'all', 'w6' or 'w12'. */
  range: '6' | '3' | 'all' | 'w6' | 'w12';
  mode: RetroInsightsMode;
  /** The columns every `counts` and `closedAt` index into. */
  columns: RetroInsightColumnDto[];
  unthemed: RetroUnthemedDto;
  mergeProposals: RetroMergeProposalDto[];
  /** Filled in sprint mode, empty in week mode. */
  sprints: SprintRef[];
  themes: RetroThemeInsightDto[];
  keepsWorking: RetroThemeInsightDto[];
  actionItems: RetroActionRowDto[];
  impact: RetroImpactDto[];
}

export interface RetroThemeOptionDto {
  theme: RetroThemeRefDto;
  pointCount: number;
}

export interface RetroThemeListDto {
  mode: RetroThemeMode;
  themes: RetroThemeOptionDto[];
}

export interface RetroCategoryCountsDto {
  wentWell: number;
  notInPlan: number;
  wentBadly: number;
  improve: number;
}

export interface RetroAggregateDto {
  counts: RetroCategoryCountsDto;
  toDecide: number;
  decided: number;
  decidable: number;
  actions: number;
}

export interface WorkItemRetroRowDto {
  retroId: string;
  author: RetroAuthorDto;
  partial: boolean;
  createdAtLabel: string;
  totals: RetroAggregateDto;
}

export interface WorkItemRetrosDto {
  /** The UI no longer reads this; it reads `rows`. */
  cards: RetroCardDto[];
  rows: WorkItemRetroRowDto[];
}

export interface RetroFeatureRowDto {
  item: RetroItemRefDto;
  state: StateRef;
  retros: number;
  ownRetros: number;
  totals: RetroAggregateDto;
  lastRetroLabel: string | null;
  noFeatureRetro: boolean;
}

export interface RetroFeaturesDto {
  features: RetroFeatureRowDto[];
  /** Stories and bugs with no Feature that have a retro. */
  loose: RetroFeatureRowDto[];
}

export interface RetroItemDto {
  item: RetroItemRefDto;
  state: StateRef;
  parent: RetroItemRefDto | null;
  totals: RetroAggregateDto;
  categories: RetroCategoryColumnDto[];
  cards: RetroCardDto[];
  actions: RetroActionRefDto[];
  /** Empty unless the item is a Feature. */
  children: RetroFeatureRowDto[];
}

export interface RetroActionDialogDto {
  selected: RetroPointDto[];
  /** Improve, wentBadly, notInPlan order; undecided only; excludes `selected`. */
  offered: RetroPointDto[];
  openActions: RetroActionRefDto[];
  suggestedTheme: RetroThemeRefDto | null;
}

export interface RetroActionDetailDto {
  action: RetroActionRefDto;
  theme: RetroThemeRefDto | null;
  points: RetroPointDto[];
  raisedIn: SprintRef | null;
}

/** Exactly one of retroId, workItemId and sprintId. */
export interface RetroPointCreate {
  retroId: string | null;
  workItemId: string | null;
  sprintId: string | null;
  category: RetroCategory;
  text: string;
  rootCause: string | null;
  themeId: string | null;
  themeName: string | null;
}

export interface RetroPointThemePut {
  themeId: string | null;
  themeName: string | null;
}

export interface RetroThemeRename {
  name: string;
}

export interface RetroThemeMerge {
  sourceIds: string[];
  targetId: string;
}

export interface RetroActionNew {
  title: string;
  fixType: RetroFixType;
  pointIds: string[];
  themeId: string | null;
  themeName: string | null;
}

export interface RetroActionAddPoints {
  pointIds: string[];
}

/** A full replacement: send the current value of the field you are not changing. */
export interface RetroActionPut {
  fixType: RetroFixType | null;
  themeId: string | null;
  themeName: string | null;
}

export interface RetroNotNeededPut {
  reason: string;
}
