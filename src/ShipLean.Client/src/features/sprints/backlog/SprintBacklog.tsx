import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import {
  useSprintBacklogRealtime,
  useSprintCapacity,
  useSprintItems,
  useSprintMetrics,
  useVelocity,
} from '../sprintsApi';
import { useTouchShell } from '../../../touch/useTouchShell';
import { sprintsTestIds as ids } from '../testIds';
import { BacklogKpis } from './BacklogKpis';
import { BacklogTable } from './BacklogTable';
import { Burndown } from './Burndown';
import { GlanceRows } from './rail/GlanceRows';
import { PointsBar } from './rail/PointsByState';
import { railSummary } from './rail/railSummary';
import { SprintRail } from './rail/SprintRail';
import { sprintStories, unparentedTasks } from './sprintStories';
import { sprintWorkingDays } from './sprintWorkingDays';

/**
 * The Sprint backlog body: the figures and burndown, then the sprint's stories and bugs with their tasks. At 900px and below
 * (SHIP-724) it is one column: the figure tiles, the points bar, the burndown, the two glance rows (the rail's lists, each in
 * a sheet), then the list.
 */
export function SprintBacklog({ project, sprint }: { project: ProjectDto; sprint: SprintDto }) {
  const { touch } = useTouchShell();
  useSprintBacklogRealtime(project.id);
  const metrics = useSprintMetrics(project.id, sprint.id);
  const capacity = useSprintCapacity(project.id, sprint.id);
  const velocity = useVelocity(project.id).data;
  const items = useSprintItems(project.id);
  const stories = items.data ? sprintStories(items.data, sprint.id) : undefined;
  const unparented = items.data ? unparentedTasks(items.data, sprint.id) : undefined;
  const summary = stories && unparented ? railSummary(stories, unparented) : undefined;
  const failed = metrics.isError || capacity.isError || items.isError;
  return (
    <div data-testid={ids.backlog} className="flex items-start gap-4 p-4 touch:p-3">
      <div className="grid min-w-0 flex-1 content-start gap-4 touch:grid-cols-[minmax(0,1fr)]">
        {failed ? (
          <p data-testid={ids.backlogLoadError} role="alert" className="m-0 text-alert">
            The sprint could not be loaded.
          </p>
        ) : null}
        {metrics.data && capacity.data ? (
          <div
            data-testid={ids.figures}
            className="flex flex-wrap items-start justify-between gap-4 touch:grid touch:grid-cols-[minmax(0,1fr)] touch:gap-2.5"
          >
            <BacklogKpis
              projectKey={project.key}
              sprint={sprint}
              metrics={metrics.data}
              capacity={capacity.data}
              velocity={
                velocity === undefined ? undefined : (velocity.override ?? velocity.average)
              }
            />
            {touch && summary ? <PointsBar points={summary.points} /> : null}
            <Burndown
              metrics={metrics.data}
              days={sprintWorkingDays(sprint.startDate, sprint.endDate, project.workingDays)}
            />
          </div>
        ) : null}
        {touch && summary ? <GlanceRows projectId={project.id} summary={summary} /> : null}
        {stories && unparented ? (
          // TODO: drop the sideways scroll once the list is the two-line rows, which fit the screen.
          <div className="min-w-0 touch:overflow-x-auto">
            <BacklogTable
              projectId={project.id}
              sprintId={sprint.id}
              stories={stories}
              unparented={unparented}
            />
          </div>
        ) : null}
        {!failed && (!items.data || !metrics.data) ? (
          <p className="m-0 text-ink-muted">Loading…</p>
        ) : null}
      </div>
      {!touch && stories && unparented ? (
        <SprintRail projectId={project.id} stories={stories} unparented={unparented} />
      ) : null}
    </div>
  );
}
