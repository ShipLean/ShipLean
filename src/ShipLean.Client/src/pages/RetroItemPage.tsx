import { useParams } from 'react-router-dom';
import { pagesTestIds } from '../features/pages/testIds';
import { RetroItemSlot } from '../slots/RetroItemSlot';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';
import { retroItemSlots } from './retroItemSlots';

export function RetroItemPage() {
  const { itemKey = '' } = useParams();
  return (
    <ProjectScreen routeId={pageRouteIds.retroItem} testId={pagesTestIds.retroItemPage}>
      {(project) => <RetroItemSlot {...retroItemSlots(project, itemKey).retroItem} />}
    </ProjectScreen>
  );
}
