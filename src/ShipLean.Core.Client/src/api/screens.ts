// The phone app's Features screens: a Feature's health, the asks waiting on the caller and the screens' fixed rules. C#:
// ShipLean.Contracts.Screens. Nothing is omitted on the wire: absent values are null and lists are []. Timestamps are ISO
// 8601 strings with a UTC offset, each next to a server-formatted label.
import type { UserRef } from './refs';

export const FEATURE_HEALTHS = ['onTrack', 'atRisk', 'blocked'] as const;
/** A Feature's health. Features only; null on other types. */
export type FeatureHealth = (typeof FEATURE_HEALTHS)[number];

/** When a Feature's health was last set, and by whom. `agent` is the agent stamp, null for a change made in the app. */
export interface HealthSetDto {
  at: string;
  atLabel: string;
  by: UserRef | null;
  agent: string | null;
}

/** Open questions and decisions waiting on the caller. A Feature's counts include its stories'; onChildren says some are there. */
export interface NeedsYouDto {
  questions: number;
  decisions: number;
  onChildren: boolean;
}

/** The screens' fixed rules. statusStaleAfterDays matches the server's FeatureRules.StatusStaleAfter. */
export const FEATURE_RULES = {
  statusStaleAfterDays: 3,
} as const;
