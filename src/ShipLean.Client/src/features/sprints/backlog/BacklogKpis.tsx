import type { SprintCapacityDto, SprintDto, SprintMetricsDto } from '@shiplean/core/api/sprints';
import { Link } from 'react-router-dom';
import { routePath } from '../../../routes';
import { useTouchShell } from '../../../touch/useTouchShell';
import { sprintsTestIds as ids } from '../testIds';
import { KpiTile } from './KpiTile';

interface BacklogKpisProps {
  projectKey: string;
  sprint: SprintDto;
  metrics: SprintMetricsDto;
  capacity: SprintCapacityDto;
  /** What a sprint with no entered capacity plans against (the project's override, else the average). */
  velocity: number | undefined;
}

/** Committed against the team's capacity (or velocity when none is entered), completed, remaining and the days left. */
export function BacklogKpis({ projectKey, sprint, metrics, capacity, velocity }: BacklogKpisProps) {
  const { touch } = useTouchShell();
  const entered = capacity.members.length > 0;
  const planned = entered ? capacity.teamTotal : velocity;
  const capacityPath = routePath('capacity', { key: projectKey, sprintId: sprint.id });
  const committed = touch ? (
    // On a phone the whole tile is the link to Capacity (SHIP-724 D7).
    <KpiTile
      testId={ids.kpiCommitted}
      value={`${metrics.committed}`}
      unit={planned === undefined ? 'pts' : `/ ${planned}`}
      label={entered ? 'Committed' : 'Committed (vel.)'}
      to={capacityPath}
      linkTestId={ids.kpiCapacityEdit}
    />
  ) : (
    <KpiTile
      testId={ids.kpiCommitted}
      value={`${metrics.committed}`}
      unit={planned === undefined ? 'pts' : `/ ${planned} pts${entered ? '' : ' · velocity'}`}
      label={
        <>
          {entered ? 'Committed / team capacity' : 'Committed / velocity'} ·{' '}
          <Link data-testid={ids.kpiCapacityEdit} to={capacityPath}>
            edit
          </Link>
        </>
      }
    />
  );
  return (
    <div
      data-testid={ids.kpiRow}
      className="flex flex-wrap items-stretch gap-3 touch:grid touch:grid-cols-4 touch:gap-1.5"
    >
      {committed}
      <KpiTile
        testId={ids.kpiCompleted}
        value={`${metrics.completed}`}
        unit="pts"
        label="Completed"
      />
      <KpiTile
        testId={ids.kpiRemaining}
        value={`${metrics.remaining}`}
        unit="pts"
        label="Remaining"
      />
      <KpiTile testId={ids.kpiDaysLeft} value={sprint.timeLabel} label="Time left" wrap />
    </div>
  );
}
