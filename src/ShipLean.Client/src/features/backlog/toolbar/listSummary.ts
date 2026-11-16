import type { BacklogRowDto } from '@shiplean/core/api/views';

/** The touch list's summary line: what the list holds on the left, what it leaves out (or how much a filter hides) on the right. */
export interface BacklogSummary {
  // "14 items", "5 Features".
  count: string;
  // "65 pts open"; empty where the level shows no points.
  detail: string;
  // "Completed hidden" or "Showing 5 of 14"; empty when nothing is left out.
  note: string;
}

const noun = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

/**
 * The summary of the item rows shown. `whole` is how many items the list holds with no filter on (undefined until it is
 * read); a list narrowed to fewer items says "Showing n of m" in place of "Completed hidden".
 */
export function summarise(
  rows: readonly BacklogRowDto[],
  options: {
    one: string;
    many: string;
    withPoints: boolean;
    includeCompleted: boolean;
    narrowed: boolean;
    whole?: number;
  },
): BacklogSummary {
  const items = rows.filter((row) => row.kind === 'item');
  const open = items
    .filter((row) => row.state?.category !== 'completed' && row.state?.category !== 'removed')
    .reduce((sum, row) => sum + (row.points ?? 0), 0);
  let note = options.includeCompleted ? '' : 'Completed hidden';
  if (options.narrowed && options.whole !== undefined && items.length !== options.whole)
    note = `Showing ${items.length} of ${options.whole}`;
  return {
    count: noun(items.length, options.one, options.many),
    detail: options.withPoints ? `${open} pts open` : '',
    note,
  };
}

/** The filter sheet's footer count: the same two numbers as the summary line, and none where the summary has none. */
export function shownOf(
  rows: readonly BacklogRowDto[],
  whole: number | undefined,
): { shown: number; whole: number } | undefined {
  if (whole === undefined) return undefined;
  const shown = rows.filter((row) => row.kind === 'item').length;
  // Nothing hidden: no count, as the summary line says no "Showing n of m" then.
  return shown === whole ? undefined : { shown, whole };
}
