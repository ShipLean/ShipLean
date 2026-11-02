import type { VelocityDto } from '../../../api/sprints';
import type { BacklogRowDto, BacklogSectionDto } from '../../../api/views';
import type { BacklogColumnId } from '../backlogApi';
import { PullLineRow } from '../pull/PullLineRow';
import { usePullLine } from '../pull/usePullLine';
import type { DropLine } from './planningTarget';
import { PlanningSectionRows } from './PlanningSectionRows';

// The rows of the Backlog section: a sprint's rows plus the pull line, drawn under the row where the forecast's cut
// falls.
export function PlanningBacklogRows({
  section,
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
  onKeyMove,
}: {
  section: BacklogSectionDto;
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
  onKeyMove: (row: BacklogRowDto, direction: 'up' | 'down') => void;
}) {
  const pull = usePullLine({ section, velocity, forecast, projectId, filtering });
  return (
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
      onKeyMove={onKeyMove}
      after={(row) =>
        pull.afterRowId === row.id ? <PullLineRow row={row} columnCount={columnCount} /> : null
      }
    />
  );
}
