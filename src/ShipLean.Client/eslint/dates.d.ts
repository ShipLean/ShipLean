import type { Rule } from 'eslint';

export interface DatesAllowlistEntry {
  /** Path from the package root, with forward slashes. */
  file: string;
  owner: string;
  why: string;
}

export const noClientDates: Rule.RuleModule;
