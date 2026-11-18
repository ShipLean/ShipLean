import type { StateRef } from '@shiplean/core/api/refs';
import type { TaskboardCellDto } from '@shiplean/core/api/views';
import type { TouchPagerPage } from '../../../../touch/TouchPager';
import { passesFaces } from '../faceFilter';

/**
 * The pager's pages: one per task state in the view's order, Removed left out. Each chip counts the tasks in that state, across
 * the given rows, that pass the face filter. `pulses` names a counter per state: a chip whose counter changed pulses once.
 */
export function stateChips(
  columns: readonly { state: StateRef }[],
  rows: readonly { cells: readonly TaskboardCellDto[] }[],
  faces: ReadonlySet<string>,
  pulses: ReadonlyMap<string, number> = new Map(),
): TouchPagerPage[] {
  return columns
    .filter((column) => column.state.category !== 'removed')
    .map(({ state }) => {
      const count = rows
        .flatMap((row) => row.cells)
        .filter((cell) => cell.stateId === state.id)
        .flatMap((cell) => cell.tasks)
        .filter((card) => passesFaces(card, faces)).length;
      const pulse = pulses.get(state.id);
      return pulse === undefined
        ? { id: state.id, label: state.name, count, category: state.category }
        : { id: state.id, label: state.name, count, category: state.category, pulse };
    });
}
