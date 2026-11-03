import { pagesTestIds } from '../features/pages/testIds';
import { NewSprintRailSlot } from '../slots/NewSprintRailSlot';
import { SprintsListSlot } from '../slots/SprintsListSlot';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';
import { sprintsSlots } from './sprintsSlots';
import { WithSprints } from './WithSprints';

export function SprintsPage() {
  return (
    <ProjectScreen routeId={pageRouteIds.sprints} testId={pagesTestIds.sprintsPage}>
      {(project) => (
        <WithSprints project={project}>
          {(sprints) => {
            const slots = sprintsSlots(project, sprints);
            return (
              <>
                <SprintsListSlot {...slots.sprintsList} />
                <NewSprintRailSlot {...slots.newSprintRail} />
              </>
            );
          }}
        </WithSprints>
      )}
    </ProjectScreen>
  );
}
