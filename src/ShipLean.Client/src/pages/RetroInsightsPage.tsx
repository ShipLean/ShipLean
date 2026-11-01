import { pagesTestIds } from '../features/pages/testIds';
import { RetroInsightsSlot } from '../slots/RetroInsightsSlot';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';
import { retroInsightsSlots } from './retroInsightsSlots';

export function RetroInsightsPage() {
  return (
    <ProjectScreen routeId={pageRouteIds.retroInsights} testId={pagesTestIds.retroInsightsPage}>
      {(project) => <RetroInsightsSlot {...retroInsightsSlots(project).retroInsights} />}
    </ProjectScreen>
  );
}
