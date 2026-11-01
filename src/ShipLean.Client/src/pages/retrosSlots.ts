import type { ProjectDto } from '../api/projects';
import type { RetrosSearchSlotProps } from '../slots/RetrosSearchSlot';

// Retrospectives › Search is one slot.
export function retrosSlots(project: ProjectDto): { retrosSearch: RetrosSearchSlotProps } {
  return { retrosSearch: { project } };
}
