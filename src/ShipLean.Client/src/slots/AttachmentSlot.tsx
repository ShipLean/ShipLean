import type { ProjectDto } from '../api/projects';
import { AttachmentScreen } from '../features/attachment/AttachmentScreen';
import { pagesTestIds } from '../features/pages/testIds';

export interface AttachmentSlotProps {
  project: ProjectDto;
  itemKey: string;
  attachmentId: string;
}

// One attachment of a work item on its own page (a Markdown file opens rendered there).
export function AttachmentSlot(props: AttachmentSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotAttachment}>
      <AttachmentScreen {...props} />
    </div>
  );
}
