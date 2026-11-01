import type { ProjectDto } from '../../../api/projects';
import type { RetroInsightsDto } from '../../../api/retros';

// What every part of the insights screen is given: the project and the loaded insights for the chosen range.
export interface RetroInsightsSlotProps {
  project: ProjectDto;
  insights: RetroInsightsDto;
}
