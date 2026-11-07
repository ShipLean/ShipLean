import type { ESLint, Rule } from 'eslint';

export interface BreakpointAllowlistEntry {
  /** Path under src/ShipLean.Client/src/, with forward slashes. */
  file: string;
  count: number;
  owner: string;
}

export function bannedVariants(text: string): string[];
export const noBreakpointVariants: Rule.RuleModule;
export const breakpointsPlugin: ESLint.Plugin;
