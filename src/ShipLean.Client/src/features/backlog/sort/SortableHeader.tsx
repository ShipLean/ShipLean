import type { CSSProperties, ReactNode } from 'react';
import { backlogTestIds as ids } from '../testIds';
import type { ColumnSort } from './columnSort';

// A column title of a list-mode table: a button that sorts by the column, with an arrow on the sorted one. Unsorted it
// looks like the plain title it replaces.
export function SortableHeader({
  column,
  sort,
  onSort,
  numeric,
  width,
  children,
}: {
  column: string;
  sort: ColumnSort;
  onSort: (column: string) => void;
  numeric: boolean;
  width: CSSProperties['width'];
  children: ReactNode;
}) {
  const direction = sort?.column === column ? sort.direction : null;
  return (
    <th
      data-testid={ids.columnHeader}
      data-column={column}
      aria-sort={direction === null ? undefined : direction === 'asc' ? 'ascending' : 'descending'}
      className={`px-2 font-semibold ${numeric ? 'text-right' : ''}`}
      style={{ width }}
    >
      <button
        type="button"
        data-testid={ids.columnSort}
        data-direction={direction ?? undefined}
        title="Sort by this column"
        className={`font-semibold hover:text-ink ${numeric ? 'text-right' : 'text-left'} ${
          direction === null ? '' : 'text-ink'
        }`}
        onClick={() => onSort(column)}
      >
        {children}
        {direction === null ? null : (
          <span aria-hidden="true" className="ml-1">
            {direction === 'asc' ? '▲' : '▼'}
          </span>
        )}
      </button>
    </th>
  );
}
