import { useParams } from 'react-router-dom';
import { pagesTestIds } from '../features/pages/testIds';
import { FormSlot } from '../slots/FormSlot';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';
import { workItemSlots } from './workItemSlots';

// One work item. Each slot reads the item itself under ['projects', projectId, 'work-items', 'by-key', itemKey].
// TODO: `?addLink=causedBy` (routes.ts, SHIP-1398 QK6) is accepted but not read yet, so the page renders unchanged; the links
// section opening its picker preset to Caused by replaces this.
export function WorkItemPage() {
  const { itemKey = '' } = useParams();
  return (
    <ProjectScreen routeId={pageRouteIds.workItem} testId={pagesTestIds.workItemPage}>
      {(project) => <FormSlot {...workItemSlots(project, itemKey).form} />}
    </ProjectScreen>
  );
}
