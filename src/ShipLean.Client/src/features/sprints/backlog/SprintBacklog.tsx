import type { ProjectDto } from '../../../api/projects';
import type { SprintDto } from '../../../api/sprints';
import {
  useSprintBacklogRealtime,
  useSprintCapacity,
  useSprintItems,
  useSprintMetrics,
  useVelocity,
} from '../sprintsApi';
import { sprintsTestIds as ids } from '../testIds';
import { BacklogKpis } from './BacklogKpis';
import { BacklogTable } from './BacklogTable';
import { Burndown } from './Burndown';
import { SprintRail } from './rail/SprintRail';
import { sprintStories, unparentedTasks } from './sprintStories';
import { sprintWorkingDays } from './sprintWorkingDays';

/** The Sprint backlog body: the figures and burndown, then the sprint's stories and bugs with their tasks. */
export function SprintBacklog({ project, sprint }: { project: ProjectDto; sprint: SprintDto }) {
  useSprintBacklogRealtime(project.id);
  const metrics = useSprintMetrics(project.id, sprint.id);
  const capacity = useSprintCapacity(project.id, sprint.id);
  const velocity = useVelocity(project.id).data;
  const items = useSprintItems(project.id);
  const stories = items.data ? sprintStories(items.data, sprint.id) : undefined;
  const unparented = items.data ? unparentedTasks(items.data, sprint.id) : undefined;
  const failed = metrics.isError || capacity.isError || items.isError;
  return (
    <div data-testid={ids.backlog} className="flex items-start gap-4 p-4">
      <div className="grid min-w-0 flex-1 content-start gap-4">
        {failed ? (
          <p data-testid={ids.backlogLoadError} role="alert" className="m-0 text-alert">
            The sprint could not be loaded.
          </p>
        ) : null}
        {metrics.data && capacity.data ? (
          <div className="flex flex-wrap items-start justify-between gap-4">
            <BacklogKpis
              projectKey={project.key}
              sprint={sprint}
              metrics={metrics.data}
              capacity={capacity.data}
              velocity={
                velocity === undefined ? undefined : (velocity.override ?? velocity.average)
              }
            />
            <Burndown
              metrics={metrics.data}
              days={sprintWorkingDays(sprint.startDate, sprint.endDate, project.workingDays)}
            />
          </div>
        ) : null}
        {stories && unparented ? (
          <BacklogTable
            projectId={project.id}
            sprintId={sprint.id}
            stories={stories}
            unparented={unparented}
          />
        ) : null}
        {!failed && (!items.data || !metrics.data) ? (
          <p className="m-0 text-ink-muted">Loading…</p>
        ) : null}
      </div>
      {stories && unparented ? (
        <SprintRail projectId={project.id} stories={stories} unparented={unparented} />
      ) : null}
    </div>
  );
}
