import { useParams, useSearchParams } from 'react-router-dom';
import { pagesTestIds } from '../features/pages/testIds';
import { FeatureBoardSettingsSlot } from '../slots/FeatureBoardSettingsSlot';
import { FeatureBoardSlot } from '../slots/FeatureBoardSlot';
import { featureBoardSlots } from './featureBoardSlots';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';

// One Feature's board. An item that is not a Feature (or does not exist) shows the not-found state from the slot.
export function FeatureBoardPage() {
  const { itemKey = '' } = useParams();
  const [params, setParams] = useSearchParams();
  return (
    <ProjectScreen routeId={pageRouteIds.featureBoard} testId={pagesTestIds.featureBoardPage}>
      {(project) => {
        const slots = featureBoardSlots(project, itemKey, params, setParams);
        return (
          <>
            <FeatureBoardSlot {...slots.featureBoard} />
            <FeatureBoardSettingsSlot {...slots.featureBoardSettings} />
          </>
        );
      }}
    </ProjectScreen>
  );
}
