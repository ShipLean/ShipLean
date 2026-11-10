import type { VelocityDto } from '@shiplean/core/api/sprints';
import type { BacklogRowDto, BacklogSectionDto } from '@shiplean/core/api/views';
import type { BacklogColumnId } from '../backlogApi';
import { PullLineRow } from '../pull/PullLineRow';
import { usePullLine } from '../pull/usePullLine';
import type { DropLine } from './planningTarget';
import { PlanningSectionRows } from './PlanningSectionRows';

// The rows of the Backlog section: a sprint's rows plus the pull line, drawn under the row where the forecast's cut
// falls.
export function PlanningBacklogRows({
  section,
  sprints,
  columns,
  columnCount,
  projectId,
  projectKey,
  marks,
  forecast,
  velocity,
  flashing,
  dropLine,
  filtering,
  addingOff,
  onKeyMove,
}: {
  section: BacklogSectionDto;
  // The sprint sections, one of which the pull line fills.
  sprints: readonly BacklogSectionDto[];
  columns: readonly BacklogColumnId[];
  columnCount: number;
  projectId: string;
  projectKey: string;
  marks: ReadonlyMap<string, string>;
  forecast: boolean;
  velocity: VelocityDto | null;
  flashing: ReadonlySet<string>;
  dropLine: DropLine | null;
  filtering: boolean;
  addingOff: boolean;
  onKeyMove: (row: BacklogRowDto, direction: 'up' | 'down') => void;
}) {
  const pull = usePullLine({ section, sprints, velocity, forecast, projectId, filtering });
  const first = section.rows[0];
  return (
    <>
      {pull.atTop && first ? (
        <PullLineRow
          line={pull}
          row={first}
          section={section}
          projectId={projectId}
          columnCount={columnCount}
        />
      ) : null}
      <PlanningSectionRows
        section={section}
        columns={columns}
        columnCount={columnCount}
        projectId={projectId}
        projectKey={projectKey}
        marks={marks}
        forecast={forecast}
        flashing={flashing}
        dropLine={dropLine}
        filtering={filtering}
        addingOff={addingOff}
        onKeyMove={onKeyMove}
        isPulled={pull.isPulled}
        after={(row) =>
          pull.afterRowId === row.id ? (
            <PullLineRow
              line={pull}
              row={row}
              section={section}
              projectId={projectId}
              columnCount={columnCount}
            />
          ) : null
        }
      />
    </>
  );
}
