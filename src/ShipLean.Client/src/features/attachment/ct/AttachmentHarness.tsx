import { useLocation } from 'react-router-dom';
import { ToastHost } from '../../../components/ToastHost';
import type { ProjectDto } from '../../../api/projects';
import { AttachmentScreen } from '../AttachmentScreen';
import { harnessLocationTestId } from './harnessIds';

const project: ProjectDto = {
  id: 'p1',
  key: 'SHIP',
  name: 'Shipping',
  description: '',
  role: 'admin',
  memberCount: 2,
  workingDays: [1, 2, 3, 4, 5],
  velocityWindow: 3,
  velocityOverride: null,
  nextItemNumber: 200,
};

// The attachment page as the slot mounts it, with the toasts and the router's path (a save moves to the new copy).
export function AttachmentHarness({
  itemKey,
  attachmentId,
}: {
  itemKey: string;
  attachmentId: string;
}) {
  const location = useLocation();
  return (
    <>
      <output data-testid={harnessLocationTestId}>{location.pathname}</output>
      <AttachmentScreen project={project} itemKey={itemKey} attachmentId={attachmentId} />
      <ToastHost />
    </>
  );
}
