import type { ReactNode } from 'react';
import type { ProjectDto } from '../api/projects';
import { WorkItemScreen } from '../features/workItem/form/WorkItemScreen';
import { pagesTestIds } from '../features/pages/testIds';

// The work item page's frame: the header and the fields strip across the full width, then the main column (the form's
// body, `tasks`, `discussion`) beside the rail (`links` at the top, then `side`), as work-item.html draws it.
export interface FormSlotProps {
  project: ProjectDto;
  itemKey: string;
  links: ReactNode;
  tasks: ReactNode;
  discussion: ReactNode;
  side: ReactNode;
}

export function FormSlot(props: FormSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotForm}>
      <WorkItemScreen {...props} />
    </div>
  );
}
