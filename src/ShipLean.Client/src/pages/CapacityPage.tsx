import { SprintHeader } from '../features/sprintHeader/SprintHeader';
import { pagesTestIds } from '../features/pages/testIds';
import { CapacitySlot } from '../slots/CapacitySlot';
import { capacitySlots } from './capacitySlots';
import { pageRouteIds } from './pageRouteIds';
import { SprintScreen } from './SprintScreen';

export function CapacityPage() {
  return (
    <SprintScreen routeId={pageRouteIds.capacity} testId={pagesTestIds.capacityPage}>
      {(project, sprint, sprints) => {
        const slots = capacitySlots(project, sprint, sprints);
        return (
          <>
            <SprintHeader {...slots.header} />
            <CapacitySlot {...slots.capacity} />
          </>
        );
      }}
    </SprintScreen>
  );
}
