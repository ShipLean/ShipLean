// Retrospective shapes (SHIP-82 contract C3). C# twin: ShipLean.Contracts.Retros. Fetchers live in retrosApi.ts.
import type { SprintRef, StateRef, TypeRef, UserRef } from './refs';

export const RETRO_CATEGORIES = ['wentWell', 'wentBadly', 'improve', 'notInPlan'] as const;
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
  action: RetroActionRefDto | null;
  canArchive: boolean;
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
}

export interface RetroCoverageRowDto {
  item: RetroItemRefDto;
  status: RetroCoverage;
}

export interface SprintRetroDto {
  sprint: SprintRef;
  kpis: RetroKpisDto;
  sprints: SprintRef[];
  /** Columns run wentBadly, improve, notInPlan, wentWell. */
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

export interface RetroInsightsDto {
  /** The requested range: '6', '3' or 'all'. */
  range: '6' | '3' | 'all';
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

export interface WorkItemRetrosDto {
  cards: RetroCardDto[];
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

/** The title defaults to the point text. */
export interface RetroActionCreate {
  title: string | null;
  points: number | null;
}
