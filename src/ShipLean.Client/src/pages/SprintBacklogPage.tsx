import { SprintHeader } from '../features/sprintHeader/SprintHeader';
import { pagesTestIds } from '../features/pages/testIds';
import { SprintBacklogSlot } from '../slots/SprintBacklogSlot';
import { sprintBacklogSlots } from './sprintBacklogSlots';
import { pageRouteIds } from './pageRouteIds';
import { SprintScreen } from './SprintScreen';

export function SprintBacklogPage() {
  return (
    <SprintScreen routeId={pageRouteIds.sprintBacklog} testId={pagesTestIds.sprintBacklogPage}>
      {(project, sprint, sprints) => {
        const slots = sprintBacklogSlots(project, sprint, sprints);
        return (
          <>
            <SprintHeader {...slots.header} />
            <SprintBacklogSlot {...slots.sprintBacklog} />
          </>
        );
      }}
    </SprintScreen>
  );
}
