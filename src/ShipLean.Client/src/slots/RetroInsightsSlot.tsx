import type { ProjectDto } from '../api/projects';
import { pagesTestIds } from '../features/pages/testIds';
import { RetroInsightsPanel } from '../features/retros/insights/RetroInsightsPanel';

export interface RetroInsightsSlotProps {
  project: ProjectDto;
}

export function RetroInsightsSlot({ project }: RetroInsightsSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotRetroInsights}>
      <RetroInsightsPanel project={project} />
    </div>
  );
}
