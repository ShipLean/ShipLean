import { pagesTestIds } from '../features/pages/testIds';
import { NewSprintRailSlot } from '../slots/NewSprintRailSlot';
import { SprintsListSlot } from '../slots/SprintsListSlot';
import { useTouchShell } from '../touch/useTouchShell';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';
import { sprintsSlots } from './sprintsSlots';
import { WithSprints } from './WithSprints';

export function SprintsPage() {
  const { touch } = useTouchShell();
  return (
    <ProjectScreen routeId={pageRouteIds.sprints} testId={pagesTestIds.sprintsPage}>
      {(project) => (
        <WithSprints project={project}>
          {(sprints) => {
            const slots = sprintsSlots(project, sprints);
            return (
              <>
                {touch ? <NewSprintRailSlot {...slots.newSprintRail} /> : null}
                <SprintsListSlot {...slots.sprintsList} />
                {touch ? null : <NewSprintRailSlot {...slots.newSprintRail} />}
              </>
            );
          }}
        </WithSprints>
      )}
    </ProjectScreen>
  );
}
