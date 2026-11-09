import { Fragment, useMemo } from 'react';
import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { BACKLOG_COLUMNS, type BacklogColumnId } from '../backlogApi';
import { backlogTestIds as ids } from '../testIds';
import { SelectAllCell } from '../selection/SelectAllCell';
import { useSelection } from '../selection/useSelection';
import { AddBelowRow } from '../add/AddBelowRow';
import type { ColumnSort } from '../sort/columnSort';
import { SortableHeader } from '../sort/SortableHeader';
import { ListRow } from './ListRow';
import { NewItemRow } from './NewItemRow';

const widths: Partial<Record<BacklogColumnId, string>> = {
  rank: '48px',
  key: '90px',
  parent: '170px',
  state: '120px',
  points: '64px',
  sprint: '110px',
  assignee: '72px',
};
const numeric: readonly BacklogColumnId[] = ['rank', 'points'];

// The ranked table: a header, the optional inline new-item row, then one row per item. Its drag and drop is ListDnd's.
export function ListTable({
  rows,
  columns,
  projectId,
  projectKey,
  adding,
  addType,
  addRequest,
  onCloseAdd,
  dragDisabled,
  filtered,
  searched = '',
  flashing,
  sort,
  onSort,
}: {
  rows: BacklogRowDto[];
  columns: readonly BacklogColumnId[];
  projectId: string;
  projectKey: string;
  adding: boolean;
  // The type the new-item row opens with (User Story when left out).
  addType?: WorkItemTypeId;
  addRequest?: number;
  onCloseAdd: () => void;
  // Reordering is off (a filter, search or check queue is on).
  dragDisabled: boolean;
  // A filter, search or check queue is on: an empty list means nothing matches it.
  filtered: boolean;
  // What the search typed, when one is on: an empty list names it.
  searched?: string;
  // Ids of the rows a teammate just changed.
  flashing: ReadonlySet<string>;
  // The column the rows are sorted by (null = rank order), and what a click on a column title does.
  sort: ColumnSort;
  onSort: (column: string) => void;
}) {
  // The select column and the grip column come before the data columns.
  const columnCount = columns.length + 2;
  const emptyText =
    searched === ''
      ? filtered
        ? 'Nothing matches this filter'
        : 'Nothing to show'
      : `Nothing matches "${searched}"`;
  const rowIds = useMemo(() => rows.map((row) => row.id), [rows]);
  useSelection(rowIds);
  return (
    <div data-testid={ids.list} className="flex-1 overflow-auto">
      <table className="w-full border-collapse">
        <thead>
          <tr className="h-8 border-b border-edge text-left text-[length:var(--text-xs)] text-ink-muted">
            <SelectAllCell />
            <th className="w-6" />
            {columns.map((column) => (
              <SortableHeader
                key={column}
                column={column}
                sort={sort}
                onSort={onSort}
                numeric={numeric.includes(column)}
                width={widths[column]}
              >
                {BACKLOG_COLUMNS.find((c) => c.id === column)?.label}
              </SortableHeader>
            ))}
          </tr>
        </thead>
        <tbody>
          {adding ? (
            <NewItemRow
              projectId={projectId}
              projectKey={projectKey}
              columnCount={columnCount}
              onClose={onCloseAdd}
              initialType={addType}
              request={addRequest}
            />
          ) : null}
          {rows.map((row) => (
            <Fragment key={row.id}>
              <ListRow
                row={row}
                columns={columns}
                projectKey={projectKey}
                dragDisabled={dragDisabled}
                flash={flashing.has(row.id)}
              />
              <AddBelowRow
                row={row}
                columnCount={columnCount}
                projectId={projectId}
                projectKey={projectKey}
                filtering={filtered}
                sorted={sort !== null}
              />
            </Fragment>
          ))}
          {rows.length === 0 ? (
            <tr>
              <td
                colSpan={columnCount}
                data-testid={ids.noRows}
                className="h-16 text-center text-[length:var(--text-sm)] text-ink-muted"
              >
                {emptyText}
              </td>
            </tr>
          ) : null}
        </tbody>
      </table>
    </div>
  );
}
