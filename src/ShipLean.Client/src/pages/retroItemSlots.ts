import type { ProjectDto } from '../api/projects';
import type { RetroItemSlotProps } from '../slots/RetroItemSlot';

// One item's retros is one slot.
export function retroItemSlots(
  project: ProjectDto,
  itemKey: string,
): { retroItem: RetroItemSlotProps } {
  return { retroItem: { project, itemKey } };
}
