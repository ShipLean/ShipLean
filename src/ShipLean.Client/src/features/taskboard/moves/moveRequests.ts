import type { CardDto, TaskboardViewDto } from '../../../api/views';
import type { TaskboardGrouping, TaskboardMoveRequest } from '../taskboardApi';
import { UNASSIGNED, UNPARENTED } from './applyMove';

export interface DropTarget {
  stateId: string;
  rowId: string;
  beforeItemId: string | null;
}

// The Unparented and Unassigned rows have no id: the request names them with a null target.
const targetOf = (rowId: string): string | null =>
  rowId === UNPARENTED || rowId === UNASSIGNED ? null : rowId;

/** The request for a drop, or null when the card would stay where it is. Only Stories cells keep an order: a People drop sends no card to go above. */
export function dropRequest(
  card: CardDto,
  grouping: TaskboardGrouping,
  fromRowId: string,
  to: DropTarget,
): TaskboardMoveRequest | null {
  const beforeItemId = grouping === 'stories' ? to.beforeItemId : null;
  const sameRow = to.rowId === fromRowId;
  if (sameRow && to.stateId === card.state.id && beforeItemId === null) return null;
  return {
    taskId: card.id,
    expectedRevision: card.revision,
    stateId: to.stateId,
    row: sameRow ? null : { grouping, target: targetOf(to.rowId) },
    beforeItemId,
  };
}

/** Pick up: assign me and start the first In Progress state, in one move. */
export function pickUpRequest(card: CardDto, stateId: string, meId: string): TaskboardMoveRequest {
  return {
    taskId: card.id,
    expectedRevision: card.revision,
    stateId,
    row: { grouping: 'people', target: meId },
    beforeItemId: null,
  };
}

/** Where Alt + an arrow key sends a card: the next column left or right, the next row up or down. Null at an edge or for any other key. */
export function keyboardTarget(
  view: TaskboardViewDto,
  grouping: TaskboardGrouping,
  card: CardDto,
  fromRowId: string,
  key: string,
): DropTarget | null {
  const columns = view.columns.map((column) => column.state.id);
  const rows =
    grouping === 'stories'
      ? view.rows.map((row) => row.parent?.id ?? UNPARENTED)
      : view.peopleRows.map((row) => row.person?.id ?? UNASSIGNED);
  const column = columns.indexOf(card.state.id);
  const row = rows.indexOf(fromRowId);
  const step = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }[key];
  if (!step || column < 0 || row < 0) return null;
  const stateId = columns[column + step[0]];
  const rowId = rows[row + step[1]];
  return stateId === undefined || rowId === undefined
    ? null
    : { stateId, rowId, beforeItemId: null };
}
