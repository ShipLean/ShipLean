import type { ProjectDto } from '../api/projects';
import { pagesTestIds } from '../features/pages/testIds';
import { DiscussionSection } from '../features/workItem/discussion/DiscussionSection';

export interface DiscussionSlotProps {
  project: ProjectDto;
  itemKey: string;
}

// The work item's discussion. Comments save at once and never touch the form's draft.
export function DiscussionSlot({ project, itemKey }: DiscussionSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotDiscussion}>
      <DiscussionSection project={project} itemKey={itemKey} />
    </div>
  );
}
