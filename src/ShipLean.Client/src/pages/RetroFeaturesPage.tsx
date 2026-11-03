import { pagesTestIds } from '../features/pages/testIds';
import { RetroFeaturesSlot } from '../slots/RetroFeaturesSlot';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';
import { retroFeaturesSlots } from './retroFeaturesSlots';

export function RetroFeaturesPage() {
  return (
    <ProjectScreen routeId={pageRouteIds.retroFeatures} testId={pagesTestIds.retroFeaturesPage}>
      {(project) => <RetroFeaturesSlot {...retroFeaturesSlots(project).retroFeatures} />}
    </ProjectScreen>
  );
}
