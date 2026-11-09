import type { ProjectDto } from '@shiplean/core/api/projects';
import { pagesTestIds } from '../features/pages/testIds';
import { RetrosSearchPanel } from '../features/retros/search/RetrosSearchPanel';

export interface RetrosSearchSlotProps {
  project: ProjectDto;
}

export function RetrosSearchSlot({ project }: RetrosSearchSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotRetrosSearch}>
      <RetrosSearchPanel project={project} />
    </div>
  );
}
