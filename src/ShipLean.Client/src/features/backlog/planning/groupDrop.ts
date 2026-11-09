import type { SprintRef } from '@shiplean/core/api/refs';
import type { BacklogRowDto, BacklogSectionDto } from '@shiplean/core/api/views';
import { changesOf } from '../bulk/changesOf';
import { moveToSprintPlan } from '../bulk/movePlan';
import type { BulkPlan } from '../bulk/useBulkRun';

/** Where a dragged selection lands in Planning. */
export type GroupDrop =
  | {
      kind: 'before';
      /** The row the block lands above; null = last. */
      beforeItemId: string | null;
      sprint: SprintRef | null;
    }
  | { kind: 'section'; sprint: SprintRef | null };

/** Whether the drop names a row of the selection itself: that drop is refused, with no move and no toast. */
export function landsOnSelection(selected: ReadonlySet<string>, overId: string): boolean {
  return selected.has(overId);
}

/**
 * The selection dropped on a row: the block lands just above it, or above the next row not in the selection when
 * `below`, in the row's section. Null when the row is in the selection.
 */
export function groupDropOnRow(
  sections: readonly BacklogSectionDto[],
  selected: ReadonlySet<string>,
  overId: string,
  below: boolean,
): GroupDrop | null {
  if (landsOnSelection(selected, overId)) return null;
  const section = sections.find((s) => s.rows.some((row) => row.id === overId));
  if (section === undefined) return null;
  if (!below) return { kind: 'before', beforeItemId: overId, sprint: section.sprint };
  const after = section.rows.slice(section.rows.findIndex((row) => row.id === overId) + 1);
  const next = after.find((row) => !selected.has(row.id));
  return { kind: 'before', beforeItemId: next?.id ?? null, sprint: section.sprint };
}

/** The selection dropped on a section's header: the sprint changes, each row keeps its rank. */
export function groupDropOnSection(
  sections: readonly BacklogSectionDto[],
  sectionId: string,
): GroupDrop | null {
  const section = sections.find((s) => s.id === sectionId);
  return section === undefined ? null : { kind: 'section', sprint: section.sprint };
}

/** What to send for the drop: `rows` come in rank order. A row already in the sprint keeps it. */
export function groupPlan(drop: GroupDrop, rows: readonly BacklogRowDto[]): BulkPlan {
  if (drop.kind === 'section') return moveToSprintPlan(rows, drop.sprint);
  const placement =
    drop.beforeItemId === null
      ? { slot: 'last' as const, beforeItemId: null }
      : { slot: 'before' as const, beforeItemId: drop.beforeItemId };
  const where = drop.sprint?.name ?? 'the Backlog';
  return {
    items: rows.map((row) => ({
      row,
      changes:
        (row.sprint?.id ?? null) === (drop.sprint?.id ?? null)
          ? null
          : changesOf({ sprint: { value: drop.sprint?.id ?? null } }),
      placement,
    })),
    label: (count) => `${count} moved to ${where}`,
    undoable: true,
  };
}

/** The rows in rank order, whatever order they were selected in; an unranked row goes last, keeping its place. */
export function inRankOrder(rows: readonly BacklogRowDto[]): BacklogRowDto[] {
  const rank = (row: BacklogRowDto) => row.rank ?? Number.POSITIVE_INFINITY;
  return rows
    .map((row, at) => ({ row, at }))
    .sort((a, b) =>
      rank(a.row) === rank(b.row) ? a.at - b.at : rank(a.row) < rank(b.row) ? -1 : 1,
    )
    .map(({ row }) => row);
}
