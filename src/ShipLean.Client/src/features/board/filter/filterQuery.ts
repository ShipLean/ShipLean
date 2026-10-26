import type { BoardColumnDto } from '../../../api/views';
import type { BoardFilterQuery } from '../boardApi';

/** The value that stands for "no one" (assignee, owner) or "none" (sprint, epic) in a filter. */
export const NONE = 'none';

/** How many parts of the filter are set (keyword, people, sprint, epic each count once). */
export function filterCount(filter: BoardFilterQuery): number {
  return [
    filter.q,
    filter.assignee?.length,
    filter.owner?.length,
    filter.sprint,
    filter.epic,
  ].filter(Boolean).length;
}

/** The cards the filter hides, over every column; they still count toward each column's WIP. */
export const hiddenCards = (columns: BoardColumnDto[]): number =>
  columns.reduce((total, column) => total + column.hiddenCount, 0);

/** The list with the id added or removed; an empty list is `undefined`, so it leaves the query. */
export function toggled(list: string[] | undefined, id: string): string[] | undefined {
  const current = list ?? [];
  const next = current.includes(id)
    ? current.filter((candidate) => candidate !== id)
    : [...current, id];
  return next.length === 0 ? undefined : next;
}
