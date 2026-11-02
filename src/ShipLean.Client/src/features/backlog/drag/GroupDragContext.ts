import { createContext, useContext } from 'react';
import type { VelocityDto } from '../../../api/sprints';
import type { BacklogRowDto, BacklogSectionDto } from '../../../api/views';
import { sprintSum, type SprintSum } from '../bulk/sprintSum';

/** A selection being dragged: its rows in rank order, and the section the pointer is over. */
export interface GroupDrag {
  rows: readonly BacklogRowDto[];
  ids: ReadonlySet<string>;
  overSectionId: string | null;
  velocity: VelocityDto | null;
}

export const GroupDragContext = createContext<GroupDrag | null>(null);

/** Whether the row is part of a selection being dragged: it dims in place. */
export function useGroupDragged(rowId: string): boolean {
  return useContext(GroupDragContext)?.ids.has(rowId) ?? false;
}

/** What the section would hold after the dragged selection joins it, while the pointer is over the section. */
export function useSectionPreview(section: BacklogSectionDto): SprintSum | null {
  const drag = useContext(GroupDragContext);
  if (drag === null || drag.overSectionId !== section.id || section.sprint === null) return null;
  return sprintSum(section, drag.rows, drag.velocity);
}
