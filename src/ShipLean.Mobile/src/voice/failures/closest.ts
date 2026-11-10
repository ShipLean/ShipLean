// The items nearest to words that matched nothing.
export interface ClosestRow {
  key: string;
  title: string;
}

export const CLOSEST_LIMIT = 2;

const words = (text: string): string[] => text.toLowerCase().match(/[a-z0-9]+/g) ?? [];

// The rows sharing the most title words with what was said, best first and then in screen order; never more than
// the limit, and none that share no word. Used offline, when search can't be asked.
export function closestRows(
  spoken: string,
  rows: readonly ClosestRow[],
  limit = CLOSEST_LIMIT,
): ClosestRow[] {
  const said = new Set(words(spoken));
  return rows
    .map((row, order) => ({
      row,
      order,
      score: [...new Set(words(row.title))].filter((w) => said.has(w)).length,
    }))
    .filter((r) => r.score > 0)
    .sort((a, b) => b.score - a.score || a.order - b.order)
    .slice(0, limit)
    .map((r) => r.row);
}

// Search's own order, kept to one project; the search has no project parameter.
export function closestFromSearch(
  items: readonly (ClosestRow & { projectKey: string })[],
  projectKey: string,
  limit = CLOSEST_LIMIT,
): ClosestRow[] {
  return items
    .filter((item) => item.projectKey === projectKey)
    .slice(0, limit)
    .map(({ key, title }) => ({ key, title }));
}

// Where the spoken words sit in the heard text, ignoring case; null when they aren't in it.
export function spanOf(
  heard: string,
  spoken: string | undefined,
): { start: number; end: number } | null {
  if (spoken === undefined || spoken === '') return null;
  const start = heard.toLowerCase().indexOf(spoken.toLowerCase());
  return start < 0 ? null : { start, end: start + spoken.length };
}
