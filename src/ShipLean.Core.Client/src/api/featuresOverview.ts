// The phone's Features overview and digest. C#: ShipLean.Contracts.Screens. Nothing is omitted on the wire: absent values are
// null and lists are [].
import { api } from './client';
import type { StateCategory, StateRef, UserRef } from './refs';
import type { FeatureHealth, NeedsYouDto } from './screens';
import type { ParentRefDto, StatusVerifiedDto } from './workItems';
import type { RollupCountDto } from './views';

/** One Feature on the overview. statusStale is true when the status line is older than FEATURE_RULES.statusStaleAfterDays. */
export interface FeatureRowDto {
  id: string;
  key: string;
  title: string;
  revision: number;
  state: StateRef;
  health: FeatureHealth | null;
  statusLine: string | null;
  statusVerified: StatusVerifiedDto | null;
  statusStale: boolean;
  rollups: RollupCountDto[];
  needsYou: NeedsYouDto | null;
  rank: number;
  /** The Feature's parent Epic. */
  epic: ParentRefDto | null;
}

/** One group of the overview. rows is [] when folded; count is the number of Features either way. */
export interface FeatureGroupDto {
  /** The camelCase StateCategory text: proposed, inProgress, resolved, completed. */
  stateCategory: StateCategory;
  label: string;
  folded: boolean;
  count: number;
  rows: FeatureRowDto[];
}

/** Groups come Active, Resolved, New, then Closed (folded). sinceLabel is made by the server. */
export interface FeaturesOverviewDto {
  projectId: string;
  sinceLabel: string;
  changedSince: number;
  groups: FeatureGroupDto[];
}

export const DIGEST_LINE_KINDS = [
  'state',
  'health',
  'statusLine',
  'comment',
  'agentWork',
  'ask',
] as const;
export type DigestLineKind = (typeof DIGEST_LINE_KINDS)[number];

/** One change in the digest. `by` is the person, agentName the agent stamp when an agent made it. */
export interface DigestLineDto {
  kind: DigestLineKind;
  text: string;
  by: UserRef | null;
  agentName: string | null;
  at: string;
  atLabel: string;
}

/** One work item with what happened to it. `type` is the work item type id. */
export interface DigestItemDto {
  id: string;
  key: string;
  type: string;
  title: string;
  lines: DigestLineDto[];
}

export interface FeatureDigestDto {
  since: string;
  sinceLabel: string;
  needsYou: DigestItemDto[];
  changed: DigestItemDto[];
  /** Fetches the next page when `more` is true. */
  cursor: string | null;
  more: boolean;
}

export const featuresKeys = {
  root: (projectId: string) => ['features', projectId] as const,
  overview: (projectId: string) => [...featuresKeys.root(projectId), 'overview'] as const,
  digest: (projectId: string, cursor: string | null) =>
    [...featuresKeys.root(projectId), 'digest', cursor] as const,
};

const featuresPath = (projectId: string) =>
  `/api/projects/${encodeURIComponent(projectId)}/features`;

export const getFeaturesOverview = (projectId: string) =>
  api.get<FeaturesOverviewDto>(`${featuresPath(projectId)}/overview`);

export const getFeatureDigest = (projectId: string, cursor: string | null = null) =>
  api.get<FeatureDigestDto>(
    `${featuresPath(projectId)}/digest${cursor === null ? '' : `?cursor=${encodeURIComponent(cursor)}`}`,
  );

/** 204. */
export const markDigestSeen = (projectId: string) =>
  api.post<void>(`${featuresPath(projectId)}/digest/seen`);
