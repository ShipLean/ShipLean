import { SprintHeader } from '../features/sprintHeader/SprintHeader';
import { pagesTestIds } from '../features/pages/testIds';
import { SprintDemoSlot } from '../slots/SprintDemoSlot';
import { pageRouteIds } from './pageRouteIds';
import { SprintScreen } from './SprintScreen';
import { sprintDemoSlots } from './sprintDemoSlots';

export function SprintDemoPage() {
  return (
    <SprintScreen routeId={pageRouteIds.sprintDemo} testId={pagesTestIds.sprintDemoPage}>
      {(project, sprint, sprints) => {
        const slots = sprintDemoSlots(project, sprint, sprints);
        return (
          <>
            <SprintHeader {...slots.header} />
            <SprintDemoSlot {...slots.sprintDemo} />
          </>
        );
      }}
    </SprintScreen>
  );
}
