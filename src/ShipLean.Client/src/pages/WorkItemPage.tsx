import { useParams } from 'react-router-dom';
import { pagesTestIds } from '../features/pages/testIds';
import { FormSlot } from '../slots/FormSlot';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';
import { useRouteQuery } from './useRouteQuery';
import { workItemSlots } from './workItemSlots';

// One work item. Each slot reads the item itself under ['projects', projectId, 'work-items', 'by-key', itemKey].
// `?addLink=causedBy` (routes.ts, SHIP-1398 QK6) opens a Bug's link picker preset to Caused by.
export function WorkItemPage() {
  const { itemKey = '' } = useParams();
  const { addLink } = useRouteQuery();
  return (
    <ProjectScreen routeId={pageRouteIds.workItem} testId={pagesTestIds.workItemPage}>
      {(project) => <FormSlot {...workItemSlots(project, itemKey, addLink).form} />}
    </ProjectScreen>
  );
}
