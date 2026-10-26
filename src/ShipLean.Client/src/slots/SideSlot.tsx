import type { ProjectDto } from '../api/projects';
import { pagesTestIds } from '../features/pages/testIds';
import { SideRail } from '../features/workItem/side/SideRail';

export interface SideSlotProps {
  project: ProjectDto;
  itemKey: string;
}

// The work item's side rail: attachments, history and Imported from.
export function SideSlot({ project, itemKey }: SideSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotSide}>
      <SideRail project={project} itemKey={itemKey} />
    </div>
  );
}
