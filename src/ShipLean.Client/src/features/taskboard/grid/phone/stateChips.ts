import type { StateRef } from '../../../../api/refs';
import type { TaskboardCellDto } from '../../../../api/views';
import type { TouchPagerPage } from '../../../../touch/TouchPager';
import { passesFaces } from '../faceFilter';

/**
 * The pager's pages: one per task state in the view's order, Removed left out. Each chip counts the tasks in that state, across
 * the given rows, that pass the face filter.
 */
export function stateChips(
  columns: readonly { state: StateRef }[],
  rows: readonly { cells: readonly TaskboardCellDto[] }[],
  faces: ReadonlySet<string>,
): TouchPagerPage[] {
  return columns
    .filter((column) => column.state.category !== 'removed')
    .map(({ state }) => ({
      id: state.id,
      label: state.name,
      count: rows
        .flatMap((row) => row.cells)
        .filter((cell) => cell.stateId === state.id)
        .flatMap((cell) => cell.tasks)
        .filter((card) => passesFaces(card, faces)).length,
    }));
}
