import { SprintHeader } from '../features/sprintHeader/SprintHeader';
import { pagesTestIds } from '../features/pages/testIds';
import { SprintRetroSlot } from '../slots/SprintRetroSlot';
import { pageRouteIds } from './pageRouteIds';
import { SprintScreen } from './SprintScreen';
import { sprintRetroSlots } from './sprintRetroSlots';

export function SprintRetroPage() {
  return (
    <SprintScreen routeId={pageRouteIds.sprintRetro} testId={pagesTestIds.sprintRetroPage}>
      {(project, sprint, sprints) => {
        const slots = sprintRetroSlots(project, sprint, sprints);
        return (
          <>
            <SprintHeader {...slots.header} />
            <SprintRetroSlot {...slots.sprintRetro} />
          </>
        );
      }}
    </SprintScreen>
  );
}
