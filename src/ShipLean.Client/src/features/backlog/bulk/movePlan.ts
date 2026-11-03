import type { SprintRef } from '../../../api/refs';
import type { BacklogRowDto } from '../../../api/views';
import { changesOf } from './changesOf';
import type { BulkPlan } from './useBulkRun';

const sprintName = (sprint: SprintRef | null) => sprint?.name ?? 'the Backlog';

/**
 * Moves rows into a sprint (null = the Backlog). Each keeps its rank, so there is no placement; rows already in it
 * are left out. `rows` come in rank order.
 */
export function moveToSprintPlan(
  rows: readonly BacklogRowDto[],
  sprint: SprintRef | null,
): BulkPlan {
  return {
    items: rows
      .filter((row) => (row.sprint?.id ?? null) !== (sprint?.id ?? null))
      .map((row) => ({
        row,
        changes: changesOf({ sprint: { value: sprint?.id ?? null } }),
        placement: null,
      })),
    label: (count) => `${count} moved to ${sprintName(sprint)}`,
    undoable: true,
  };
}

/** Moves rows to the top or bottom of the backlog as one block in rank order; position only. */
export function moveToEdgePlan(rows: readonly BacklogRowDto[], slot: 'first' | 'last'): BulkPlan {
  return {
    items: rows.map((row) => ({ row, changes: null, placement: { slot, beforeItemId: null } })),
    label: (count) => `${count} moved to the ${slot === 'first' ? 'top' : 'bottom'}`,
    undoable: true,
  };
}

/**
 * Planning's Top and Bottom: the block goes to the top or bottom of the backlog, so a row in a sprint also leaves its
 * sprint and a row already in the Backlog changes position only.
 */
export function moveToBacklogEdgePlan(
  rows: readonly BacklogRowDto[],
  slot: 'first' | 'last',
): BulkPlan {
  return {
    items: rows.map((row) => ({
      row,
      changes: row.sprint === null ? null : changesOf({ sprint: { value: null } }),
      placement: { slot, beforeItemId: null },
    })),
    label: (count) => `${count} moved to the ${slot === 'first' ? 'top' : 'bottom'} of the backlog`,
    undoable: true,
  };
}

/**
 * Drops rows as one block just above `beforeItemId`, in rank order, with one placement for all of them. `sprint`
 * is the section they land in: undefined leaves each row's sprint alone, null is the Backlog. The target row is
 * never part of its own block.
 */
export function moveBeforePlan(
  rows: readonly BacklogRowDto[],
  beforeItemId: string,
  sprint: SprintRef | null | undefined,
): BulkPlan {
  return {
    items: rows
      .filter((row) => row.id !== beforeItemId)
      .map((row) => ({
        row,
        changes: sprint === undefined ? null : changesOf({ sprint: { value: sprint?.id ?? null } }),
        placement: { slot: 'before' as const, beforeItemId },
      })),
    label: (count) =>
      sprint === undefined ? `${count} moved` : `${count} moved to ${sprintName(sprint)}`,
    undoable: true,
  };
}
