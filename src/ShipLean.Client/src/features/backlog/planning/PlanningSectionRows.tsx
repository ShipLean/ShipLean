import type { BacklogRowDto, BacklogSectionDto } from '../../../api/views';
import type { BacklogColumnId } from '../backlogApi';
import { PlanningRow } from './PlanningRow';
import { EmptySection } from './EmptySection';
import type { DropLine } from './planningTarget';

// The rows of one section (sprint or Backlog), or the hint a sprint with none shows (a place to drop a row). Its
// header is the caller's.
export function PlanningSectionRows({
  section,
  columns,
  columnCount,
  projectKey,
  marks,
  forecast,
  flashing,
  dropLine,
  onKeyMove,
}: {
  section: BacklogSectionDto;
  columns: readonly BacklogColumnId[];
  columnCount: number;
  projectKey: string;
  marks: ReadonlyMap<string, string>;
  forecast: boolean;
  flashing: ReadonlySet<string>;
  dropLine: DropLine | null;
  onKeyMove: (row: BacklogRowDto, direction: 'up' | 'down') => void;
}) {
  if (section.rows.length === 0 && section.sprint !== null) {
    return <EmptySection section={section} columnCount={columnCount} />;
  }
  return (
    <>
      {section.rows.map((row) => (
        <PlanningRow
          key={row.id}
          row={row}
          sectionId={section.id}
          columns={columns}
          projectKey={projectKey}
          mark={marks.get(row.id)}
          forecast={forecast}
          flash={flashing.has(row.id)}
          dropEdge={dropLine?.id === row.id ? (dropLine.below ? 'bottom' : 'top') : null}
          onKeyMove={onKeyMove}
        />
      ))}
    </>
  );
}
