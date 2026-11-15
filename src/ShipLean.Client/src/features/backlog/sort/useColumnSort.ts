import { useMemo, useState } from 'react';
import { postBacklogSort } from '@shiplean/core/api/usageApi';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import type { RouteId } from '../../../routes';
import { nextSort, sortRows, type ColumnSort } from './columnSort';

/**
 * A list-mode table's column sort, held by the screen like its filter, so leaving list mode drops it. A sort on a column
 * the table no longer shows is rank order. Each click is reported for usage, fire and forget.
 */
export function useColumnSort(
  projectId: string,
  screen: RouteId,
  rows: BacklogRowDto[],
  columns: readonly string[],
) {
  const [chosen, setChosen] = useState<ColumnSort>(null);
  const sort = chosen !== null && columns.includes(chosen.column) ? chosen : null;
  const sorted = useMemo(() => sortRows(rows, sort), [rows, sort]);
  const report = (column: string, next: ColumnSort) => {
    setChosen(next);
    postBacklogSort(projectId, { screen, column, direction: next?.direction ?? 'off' }).catch(
      () => undefined,
    );
  };
  const onSort = (column: string) => report(column, nextSort(sort, column));
  // Back to rank order from wherever the sort is: what a phone, with no column headers, offers.
  const clear = () => {
    if (sort) report(sort.column, null);
  };
  return { sort, onSort, clear, rows: sorted, sorted: sort !== null };
}
