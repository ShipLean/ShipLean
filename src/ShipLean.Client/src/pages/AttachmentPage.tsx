import { useParams } from 'react-router-dom';
import { pagesTestIds } from '../features/pages/testIds';
import { AttachmentSlot } from '../slots/AttachmentSlot';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';
import { attachmentSlots } from './workItemSlots';

// One attachment of a work item, at a linkable address under the item.
export function AttachmentPage() {
  const { itemKey = '', attachmentId = '' } = useParams();
  return (
    <ProjectScreen routeId={pageRouteIds.workItemAttachment} testId={pagesTestIds.attachmentPage}>
      {(project) => (
        <AttachmentSlot {...attachmentSlots(project, itemKey, attachmentId).attachment} />
      )}
    </ProjectScreen>
  );
}
