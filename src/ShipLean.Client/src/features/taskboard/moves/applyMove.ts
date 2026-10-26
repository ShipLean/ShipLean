import type { UserRef } from '../../../api/refs';
import type { CardDto, TaskboardCellDto, TaskboardViewDto } from '../../../api/views';
import type { TaskboardGrouping } from '../taskboardApi';

/** Where a card lands: the destination row (a story or bug id, a person id, or a sentinel), column, and the card it goes above. */
export interface AppliedMove {
  taskId: string;
  stateId: string;
  rowId: string;
  beforeItemId: string | null;
  /** The card's assignee after the move; undefined leaves it. In People rows the row's person wins, and a missing row of this person is made. */
  assignee?: UserRef | null;
}

export const UNPARENTED = 'unparented';
export const UNASSIGNED = 'unassigned';

interface Row {
  cells: TaskboardCellDto[];
}

function place<R extends Row>(
  existing: R[],
  idOf: (row: R) => string,
  adopt: (row: R, card: CardDto) => CardDto,
  move: AppliedMove,
  state: CardDto['state'],
  made: R | null,
): R[] | null {
  const rows =
    made && !existing.some((row) => idOf(row) === move.rowId) ? [...existing, made] : existing;
  const card = rows
    .flatMap((row) => row.cells)
    .flatMap((cell) => cell.tasks)
    .find((task) => task.id === move.taskId);
  if (!card || !rows.some((row) => idOf(row) === move.rowId)) return null;
  return rows.map((row) => {
    const without = row.cells.map((cell) => ({
      ...cell,
      tasks: cell.tasks.filter((task) => task.id !== move.taskId),
    }));
    if (idOf(row) !== move.rowId) return { ...row, cells: without };
    const adopted = adopt(row, card);
    const moved: CardDto = {
      ...adopted,
      state,
      assignee: move.assignee === undefined ? adopted.assignee : move.assignee,
    };
    const cells = without.some((cell) => cell.stateId === move.stateId)
      ? without
      : [...without, { stateId: move.stateId, tasks: [] }];
    return {
      ...row,
      cells: cells.map((cell) => {
        if (cell.stateId !== move.stateId) return cell;
        const at = move.beforeItemId
          ? cell.tasks.findIndex((task) => task.id === move.beforeItemId)
          : -1;
        const tasks = [...cell.tasks];
        tasks.splice(at < 0 ? tasks.length : at, 0, moved);
        return { ...cell, tasks };
      }),
    };
  });
}

/** The view with the card already where the drop put it, in the rows of the grouping on screen. A destination row the view lacks leaves the view as it is. */
export function applyMove(
  view: TaskboardViewDto,
  grouping: TaskboardGrouping,
  move: AppliedMove,
): TaskboardViewDto {
  const state = view.columns.find((column) => column.state.id === move.stateId)?.state;
  if (!state) return view;
  const moved =
    grouping === 'stories'
      ? place(
          view.rows,
          (row) => row.parent?.id ?? UNPARENTED,
          (row, card) => ({
            ...card,
            parent: row.parent
              ? {
                  id: row.parent.id,
                  key: row.parent.key,
                  type: row.parent.type,
                  title: row.parent.title,
                }
              : null,
          }),
          move,
          state,
          null,
        )
      : place(
          view.peopleRows,
          (row) => row.person?.id ?? UNASSIGNED,
          (row, card) => ({ ...card, assignee: row.person }),
          move,
          state,
          move.assignee && move.assignee.id === move.rowId
            ? { person: move.assignee, taskCount: 0, cells: [] }
            : null,
        );
  if (!moved) return view;
  const rows = grouping === 'stories' ? (moved as TaskboardViewDto['rows']) : view.rows;
  const peopleRows =
    grouping === 'people'
      ? (moved as TaskboardViewDto['peopleRows']).map((row) => ({
          ...row,
          taskCount: row.cells.reduce((n, cell) => n + cell.tasks.length, 0),
        }))
      : view.peopleRows;
  const shown = (grouping === 'stories' ? rows : peopleRows).flatMap((row) => row.cells);
  return {
    ...view,
    rows,
    peopleRows,
    columns: view.columns.map((column) => ({
      ...column,
      count: shown
        .filter((cell) => cell.stateId === column.state.id)
        .reduce((n, cell) => n + cell.tasks.length, 0),
    })),
  };
}

/** Marks a story's row as offering Move to Resolved (the move response said so; the refetch has the last word). */
export function offerResolveOn(view: TaskboardViewDto, parentId: string): TaskboardViewDto {
  return {
    ...view,
    rows: view.rows.map((row) =>
      row.parent?.id === parentId ? { ...row, offerResolve: true } : row,
    ),
  };
}

/** Puts the revision the server wrote onto the card, so the next move of it names the right one. */
export function withRevision(
  view: TaskboardViewDto,
  taskId: string,
  revision: number,
): TaskboardViewDto {
  const set = (cells: TaskboardCellDto[]) =>
    cells.map((cell) => ({
      ...cell,
      tasks: cell.tasks.map((task) => (task.id === taskId ? { ...task, revision } : task)),
    }));
  return {
    ...view,
    rows: view.rows.map((row) => ({ ...row, cells: set(row.cells) })),
    peopleRows: view.peopleRows.map((row) => ({ ...row, cells: set(row.cells) })),
  };
}

/** The revision the board holds for a card, or undefined when it is not on the board. */
export function revisionOf(view: TaskboardViewDto | undefined, taskId: string): number | undefined {
  return [...(view?.rows ?? []), ...(view?.peopleRows ?? [])]
    .flatMap((row) => row.cells)
    .flatMap((cell) => cell.tasks)
    .find((task) => task.id === taskId)?.revision;
}

/** The move that puts a card back where it was (its order within the cell comes back with the next refetch). */
export function undoOf(card: CardDto, fromRowId: string): AppliedMove {
  return {
    taskId: card.id,
    stateId: card.state.id,
    rowId: fromRowId,
    beforeItemId: null,
    assignee: card.assignee,
  };
}
