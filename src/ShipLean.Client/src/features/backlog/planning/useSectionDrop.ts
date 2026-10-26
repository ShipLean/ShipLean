import { useDroppable } from '@dnd-kit/core';
import { sectionDropId } from './planningTarget';

/** Makes a section's header (or its empty hint) a place a row can be dropped to change only its sprint. */
export function useSectionDrop(sectionId: string, part: 'header' | 'hint' = 'header') {
  const { setNodeRef, isOver } = useDroppable({ id: sectionDropId(sectionId, part) });
  return { attach: setNodeRef, over: isOver };
}
