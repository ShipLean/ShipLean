import { SPRINT_STATUSES, STATE_CATEGORIES } from '../../../api/refs';
import type { BacklogRowDto } from '../../../api/views';
import { countStoryLevelUnder, countUnder } from '../levels/countUnder';

export type SortDirection = 'asc' | 'desc';

/** The column a list is sorted by. null = rank order, the list's own order, where dragging works. */
export type ColumnSort = { column: string; direction: SortDirection } | null;

/**
 * The sort after a click on a column's title: a new column sorts ascending, the sorted one turns descending, and a third
 * click goes back to rank order. # ascending is rank order itself, so on # the first click sorts descending.
 */
export function nextSort(current: ColumnSort, column: string): ColumnSort {
  if (current?.column !== column)
    return column === 'rank' ? { column, direction: 'desc' } : { column, direction: 'asc' };
  return current.direction === 'asc' ? { column, direction: 'desc' } : null;
}

// A row's value in a column: a number or a text compare by their own rules, and null (an empty cell) always sorts last.
type SortValue = number | string | null;

const collator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });

const keyNumber = (key: string) => Number(key.slice(key.lastIndexOf('-') + 1));

const valueOf: Record<string, (row: BacklogRowDto) => SortValue> = {
  rank: (row) => row.rank,
  key: (row) => keyNumber(row.key),
  title: (row) => row.title,
  parent: (row) => row.parent?.title ?? null,
  // States sort by where they sit in the workflow (proposed first), then by name within a category.
  state: (row) =>
    row.state === null ? null : `${STATE_CATEGORIES.indexOf(row.state.category)} ${row.state.name}`,
  points: (row) => row.points,
  // Sprints sort past, current, future, then by name ("Sprint 9" before "Sprint 10").
  sprint: (row) =>
    row.sprint === null ? null : `${SPRINT_STATUSES.indexOf(row.sprint.status)} ${row.sprint.name}`,
  assignee: (row) => row.assignee?.name ?? null,
  tags: (row) => (row.tags.length === 0 ? null : row.tags.map((tag) => tag.name).join(' ')),
  features: (row) => countUnder(row, 'feature').total,
  // The share done; a row with nothing under it has no progress to compare.
  progress: (row) => {
    const { closed, total } = countStoryLevelUnder(row);
    return total === 0 ? null : closed / total;
  },
};

const compareValues = (a: number | string, b: number | string) =>
  typeof a === 'number' && typeof b === 'number' ? a - b : collator.compare(String(a), String(b));

const rankOf = (row: BacklogRowDto) => row.rank ?? Number.MAX_SAFE_INTEGER;

/** The rows in the sort's order (rank breaks ties), or the rows as given for rank order or a column it can't sort. */
export function sortRows(rows: BacklogRowDto[], sort: ColumnSort): BacklogRowDto[] {
  const value = sort === null ? undefined : valueOf[sort.column];
  if (sort === null || value === undefined) return rows;
  const sign = sort.direction === 'asc' ? 1 : -1;
  return [...rows].sort((a, b) => {
    const left = value(a);
    const right = value(b);
    if (left === null || right === null) {
      if (left !== right) return left === null ? 1 : -1;
    } else {
      const order = compareValues(left, right);
      if (order !== 0) return sign * order;
    }
    return rankOf(a) - rankOf(b);
  });
}
