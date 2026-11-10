import type { ProjectDto } from '@shiplean/core/api/projects';
import { pagesTestIds } from '../features/pages/testIds';
import { RetroItemPanel } from '../features/retros/item/RetroItemPanel';

export interface RetroItemSlotProps {
  project: ProjectDto;
  itemKey: string;
}

export function RetroItemSlot({ project, itemKey }: RetroItemSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotRetroItem}>
      <RetroItemPanel project={project} itemKey={itemKey} />
    </div>
  );
}
