import type { SprintRef } from '../../../api/refs';
import type { BacklogSectionDto } from '../../../api/views';

/** Where a dragged row goes in Planning, and what to send for it. */
export interface PlanningDrop {
  sectionId: string;
  sprint: SprintRef | null;
  /** The row leaves its sprint (or the Backlog) for this section's. */
  sprintChanged: boolean;
  /** The item the row goes above, null for last, undefined to send no reorder (a header drop keeps the order). */
  beforeItemId: string | null | undefined;
  /** Where the row shows at once: above this item, or last. */
  placeBefore: string | null;
  /** The toast after the write. */
  done: string;
  /** The toast when the sprint changed but the place could not be saved. */
  notSaved: string;
}

/** Where the drop line is drawn: on the edge of this row. */
export interface DropLine {
  id: string;
  below: boolean;
}

const SECTION_DROP_PREFIX = 'section:';

/** The id a section's header (or, for a sprint with no rows, its hint) registers as a drop target under. */
export const sectionDropId = (sectionId: string, part: 'header' | 'hint' = 'header') =>
  `${SECTION_DROP_PREFIX}${sectionId}:${part}`;

/** The section a drop target id stands for, or undefined when it is a row. */
export function sectionOfDropId(dropId: string): string | undefined {
  return dropId.startsWith(SECTION_DROP_PREFIX)
    ? dropId.slice(SECTION_DROP_PREFIX.length).split(':')[0]
    : undefined;
}

/** The sections as the table draws them: the sprints, then the Backlog. */
export function displayOrder(sections: readonly BacklogSectionDto[]): BacklogSectionDto[] {
  return [
    ...sections.filter((s) => s.sprint !== null),
    ...sections.filter((s) => s.sprint === null),
  ];
}

function locate(sections: readonly BacklogSectionDto[], itemId: string) {
  for (const section of sections) {
    const index = section.rows.findIndex((row) => row.id === itemId);
    if (index >= 0) return { section, index };
  }
  return undefined;
}

function destination(
  itemKey: string,
  from: BacklogSectionDto,
  target: BacklogSectionDto,
  beforeItemId: string | null | undefined,
  placeBefore: string | null,
): PlanningDrop {
  const sprintChanged = from.id !== target.id;
  const where = target.sprint === null ? 'Backlog' : target.title;
  return {
    sectionId: target.id,
    sprint: target.sprint,
    sprintChanged,
    beforeItemId,
    placeBefore,
    done: sprintChanged ? `${itemKey} → ${where}` : `${itemKey} reordered`,
    notSaved: `${itemKey} moved to ${where}; its place was not saved`,
  };
}

/**
 * A row dropped on another row: just above it, or just below it when `below`. Null when nothing changes (the row
 * is dropped on itself or lands where it already is).
 */
export function dropOnRow(
  sections: readonly BacklogSectionDto[],
  activeId: string,
  overId: string,
  below: boolean,
): PlanningDrop | null {
  const from = locate(sections, activeId);
  const over = locate(sections, overId);
  if (from === undefined || over === undefined || activeId === overId) return null;
  const rest = over.section.rows.filter((row) => row.id !== activeId);
  const overAt = rest.findIndex((row) => row.id === overId);
  const before = rest[below ? overAt + 1 : overAt]?.id ?? null;
  const unmoved =
    from.section.id === over.section.id &&
    before === (from.section.rows[from.index + 1]?.id ?? null);
  if (unmoved) return null;
  const key = from.section.rows[from.index].key;
  return destination(key, from.section, over.section, before, before);
}

/** A row dropped on a section's header (or its empty hint): the sprint changes, the order is left alone. */
export function dropOnSection(
  sections: readonly BacklogSectionDto[],
  activeId: string,
  sectionId: string,
): PlanningDrop | null {
  const from = locate(sections, activeId);
  const target = sections.find((s) => s.id === sectionId);
  if (from === undefined || target === undefined || from.section.id === target.id) return null;
  return destination(from.section.rows[from.index].key, from.section, target, undefined, null);
}

/**
 * Alt+Up or Alt+Down: one place along the page. Past the first or last row of a section the row crosses into the
 * next section there (last place going up, first place going down).
 */
export function dropByKey(
  sections: readonly BacklogSectionDto[],
  activeId: string,
  direction: 'up' | 'down',
): PlanningDrop | null {
  const from = locate(sections, activeId);
  if (from === undefined) return null;
  const { section, index } = from;
  const key = section.rows[index].key;
  const order = displayOrder(sections);
  const at = order.findIndex((s) => s.id === section.id);
  if (direction === 'up') {
    if (index > 0) {
      const before = section.rows[index - 1].id;
      return destination(key, section, section, before, before);
    }
    const previous = order[at - 1];
    if (previous === undefined) return null;
    return destination(key, section, previous, previous.rows.length === 0 ? undefined : null, null);
  }
  if (index < section.rows.length - 1) {
    const before = section.rows[index + 2]?.id ?? null;
    return destination(key, section, section, before, before);
  }
  const next = order[at + 1];
  if (next === undefined) return null;
  const first = next.rows[0]?.id;
  return destination(key, section, next, first, first ?? null);
}
