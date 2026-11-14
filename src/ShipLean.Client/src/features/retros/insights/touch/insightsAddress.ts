import { DEFS_ENTRIES, type DefsEntry } from '../tips/tipWording';

// The Insights screen's lane-internal address at touch (SHIP-731): `sheet=range`, `sheet=defs[:<entry>]`,
// `sheet=theme:<themeId>`, and `merge=` or `merge=<themeId>`. Written with `replace`, read at touch only.

export type InsightsSheet =
  | { kind: 'range' }
  | { kind: 'defs'; entry: DefsEntry | null }
  | { kind: 'theme'; themeId: string };

/** The sheet the address names; one this screen doesn't know is none. */
export function readSheet(params: URLSearchParams): InsightsSheet | null {
  const value = params.get('sheet');
  if (value === null) return null;
  if (value === 'range') return { kind: 'range' };
  if (value === 'defs') return { kind: 'defs', entry: null };
  if (value.startsWith('defs:')) {
    const entry = value.slice('defs:'.length);
    return { kind: 'defs', entry: DEFS_ENTRIES.includes(entry) ? entry : null };
  }
  if (value.startsWith('theme:') && value.length > 'theme:'.length) {
    return { kind: 'theme', themeId: value.slice('theme:'.length) };
  }
  return null;
}

/** Merge themes as the address names it: open, with the theme to merge pre-picked when there is one. */
export function readMerge(params: URLSearchParams): { themeId: string | null } | null {
  const value = params.get('merge');
  if (value === null) return null;
  return { themeId: value === '' ? null : value };
}

/** A copy of `params` with the sheet set to `value`, or removed for null. */
export function withSheet(params: URLSearchParams, value: string | null): URLSearchParams {
  const next = new URLSearchParams(params);
  if (value === null) next.delete('sheet');
  else next.set('sheet', value);
  return next;
}

/** The `sheet` value that opens What these mean, at an entry or at its top. */
export const defsSheetValue = (entry?: DefsEntry): string =>
  entry === undefined ? 'defs' : `defs:${entry}`;

/** A copy of `params` with `merge` set (empty for no pre-picked theme). */
export function withMerge(params: URLSearchParams, themeId?: string): URLSearchParams {
  const next = new URLSearchParams(params);
  next.set('merge', themeId ?? '');
  return next;
}
