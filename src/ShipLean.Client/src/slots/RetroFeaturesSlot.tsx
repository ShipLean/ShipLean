import type { ProjectDto } from '../api/projects';
import { pagesTestIds } from '../features/pages/testIds';
import { RetroFeaturesPanel } from '../features/retros/features/RetroFeaturesPanel';

export interface RetroFeaturesSlotProps {
  project: ProjectDto;
}

export function RetroFeaturesSlot({ project }: RetroFeaturesSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotRetroFeatures}>
      <RetroFeaturesPanel project={project} />
    </div>
  );
}
