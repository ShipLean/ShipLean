import { Fragment, type ReactNode } from 'react';
import type { BacklogRowDto, BacklogSectionDto } from '../../../api/views';
import type { BacklogColumnId } from '../backlogApi';
import { AddBelowRow } from '../add/AddBelowRow';
import { PlanningRow } from './PlanningRow';
import { EmptySection } from './EmptySection';
import type { DropLine } from './planningTarget';

// The rows of one section (sprint or Backlog), or the hint a sprint with none shows (a place to drop a row). Its
// header is the caller's.
export function PlanningSectionRows({
  section,
  columns,
  columnCount,
  projectId,
  projectKey,
  marks,
  forecast,
  flashing,
  dropLine,
  filtering,
  onKeyMove,
  isPulled,
  after,
}: {
  section: BacklogSectionDto;
  columns: readonly BacklogColumnId[];
  columnCount: number;
  projectId: string;
  projectKey: string;
  marks: ReadonlyMap<string, string>;
  forecast: boolean;
  flashing: ReadonlySet<string>;
  dropLine: DropLine | null;
  // A filter, search or check queue is on: moving is off, and rank is relative to rows you can't see.
  filtering: boolean;
  onKeyMove: (row: BacklogRowDto, direction: 'up' | 'down') => void;
  // The row sits above the pull line: its numbers take the pull-in tint. Nothing is pulled when absent.
  isPulled?: (rowId: string) => boolean;
  // What goes under a row, after its add-below row (the pull line).
  after?: (row: BacklogRowDto) => ReactNode;
}) {
  if (section.rows.length === 0 && section.sprint !== null) {
    return <EmptySection section={section} columnCount={columnCount} />;
  }
  return (
    <>
      {section.rows.map((row) => (
        <Fragment key={row.id}>
          <PlanningRow
            row={row}
            sectionId={section.id}
            columns={columns}
            projectKey={projectKey}
            mark={marks.get(row.id)}
            forecast={forecast}
            flash={flashing.has(row.id)}
            pulled={isPulled?.(row.id) ?? false}
            dropEdge={dropLine?.id === row.id ? (dropLine.below ? 'bottom' : 'top') : null}
            dragDisabled={filtering}
            onKeyMove={onKeyMove}
          />
          <AddBelowRow
            row={row}
            columnCount={columnCount}
            projectId={projectId}
            projectKey={projectKey}
            filtering={filtering}
          />
          {after?.(row)}
        </Fragment>
      ))}
    </>
  );
}
