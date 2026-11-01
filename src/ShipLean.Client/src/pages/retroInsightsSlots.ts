import type { ProjectDto } from '../api/projects';
import type { RetroInsightsSlotProps } from '../slots/RetroInsightsSlot';

// Retrospectives › Insights is one slot.
export function retroInsightsSlots(project: ProjectDto): {
  retroInsights: RetroInsightsSlotProps;
} {
  return { retroInsights: { project } };
}
