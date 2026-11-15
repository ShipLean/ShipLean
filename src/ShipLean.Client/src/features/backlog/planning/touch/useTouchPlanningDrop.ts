import { useMemo, useState } from 'react';
import type { VelocityDto } from '@shiplean/core/api/sprints';
import type { BacklogRowDto, BacklogSectionDto } from '@shiplean/core/api/views';
import { sprintSum, type SprintSum } from '../../bulk/sprintSum';
import { dropOnRow, dropOnSection, sectionOfDropId, type PlanningDrop } from '../planningTarget';

/** The section header the finger is over while a row is lifted: it lights with the sum the sprint would come to. */
export interface LitSection {
  sectionId: string;
  /** What the sprint would hold; null over the Backlog, which has no load. */
  sum: SprintSum | null;
  message: string;
}

export interface TouchPlanningDrop {
  lit: LitSection | null;
  /** The finger moved over `targetId` (a row's id or a section's drop id) with `rowId` lifted; null when it is over nothing. */
  over: (rowId: string, targetId: string | null) => void;
  /** The finger let go on `targetId`. Sends the move through `onMove`, the one write path Planning has. */
  drop: (rowId: string, targetId: string | null) => void;
}

function find(sections: readonly BacklogSectionDto[], rowId: string) {
  for (const section of sections) {
    const row = section.rows.find((r) => r.id === rowId);
    if (row !== undefined) return { section, row };
  }
  return undefined;
}

/**
 * Touch drops in Planning (SHIP-716 D2). A drop on a sprint header changes the sprint and leaves the order alone
 * (dropOnSection); a drop on a row goes just above it (dropOnRow). Either way it goes through PlanningMode's `move`,
 * the path the desktop drag uses, so there is one write and one place the rows are put.
 */
export function useTouchPlanningDrop({
  sections,
  velocity,
  onMove,
}: {
  sections: readonly BacklogSectionDto[];
  velocity: VelocityDto | null;
  onMove: (row: BacklogRowDto, drop: PlanningDrop) => void;
}): TouchPlanningDrop {
  const [hover, setHover] = useState<{ rowId: string; targetId: string } | null>(null);

  const lit = useMemo<LitSection | null>(() => {
    if (hover === null) return null;
    const sectionId = sectionOfDropId(hover.targetId);
    const from = find(sections, hover.rowId);
    const target = sections.find((s) => s.id === sectionId);
    if (from === undefined || target === undefined || from.section.id === target.id) return null;
    return {
      sectionId: target.id,
      sum: target.sprint === null ? null : sprintSum(target, [from.row], velocity),
      message: `Let go to move ${from.row.key} into ${target.title}`,
    };
  }, [hover, sections, velocity]);

  return {
    lit,
    over: (rowId, targetId) => setHover(targetId === null ? null : { rowId, targetId }),
    drop: (rowId, targetId) => {
      setHover(null);
      const from = find(sections, rowId);
      if (from === undefined || targetId === null) return;
      const sectionId = sectionOfDropId(targetId);
      const drop =
        sectionId === undefined
          ? dropOnRow(sections, rowId, targetId, false)
          : dropOnSection(sections, rowId, sectionId);
      if (drop !== null) onMove(from.row, drop);
    },
  };
}
