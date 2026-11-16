import { useParams, useSearchParams } from 'react-router-dom';
import { pagesTestIds } from '../features/pages/testIds';
import { FeatureTimelineSlot } from '../slots/FeatureTimelineSlot';
import { featureTimelineSlots } from './featureTimelineSlots';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';

// One Feature's timeline. An item that is not a Feature (or does not exist) shows the failure state from the slot.
export function FeatureTimelinePage() {
  const { itemKey = '' } = useParams();
  const [params, setParams] = useSearchParams();
  return (
    <ProjectScreen routeId={pageRouteIds.featureTimeline} testId={pagesTestIds.featureTimelinePage}>
      {(project) => {
        const slots = featureTimelineSlots(project, itemKey, params, setParams);
        return <FeatureTimelineSlot {...slots.featureTimeline} />;
      }}
    </ProjectScreen>
  );
}
