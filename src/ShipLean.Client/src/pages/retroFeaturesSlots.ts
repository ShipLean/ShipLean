import type { ProjectDto } from '@shiplean/core/api/projects';
import type { RetroFeaturesSlotProps } from '../slots/RetroFeaturesSlot';

// Retrospectives › Features is one slot.
export function retroFeaturesSlots(project: ProjectDto): { retroFeatures: RetroFeaturesSlotProps } {
  return { retroFeatures: { project } };
}
