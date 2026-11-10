import type { ProjectDto } from '@shiplean/core/api/projects';
import type { RetroInsightsDto } from '@shiplean/core/api/retros';

// What every part of the insights screen is given: the project and the loaded insights for the chosen range.
export interface RetroInsightsSlotProps {
  project: ProjectDto;
  insights: RetroInsightsDto;
}
