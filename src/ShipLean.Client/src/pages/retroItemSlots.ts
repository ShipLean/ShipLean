import type { ProjectDto } from '@shiplean/core/api/projects';
import type { RetroItemSlotProps } from '../slots/RetroItemSlot';

// One item's retros is one slot.
export function retroItemSlots(
  project: ProjectDto,
  itemKey: string,
): { retroItem: RetroItemSlotProps } {
  return { retroItem: { project, itemKey } };
}
