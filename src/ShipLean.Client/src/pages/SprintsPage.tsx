import { pagesTestIds } from '../features/pages/testIds';
import { NewSprintRailSlot } from '../slots/NewSprintRailSlot';
import { SprintEndPromptSlot } from '../slots/SprintEndPromptSlot';
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
                <SprintEndPromptSlot {...slots.sprintEndPrompt} />
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
