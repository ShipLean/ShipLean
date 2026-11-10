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
  const onSort = (column: string) => {
    const next = nextSort(sort, column);
    setChosen(next);
    postBacklogSort(projectId, { screen, column, direction: next?.direction ?? 'off' }).catch(
      () => undefined,
    );
  };
  return { sort, onSort, rows: sorted, sorted: sort !== null };
}
