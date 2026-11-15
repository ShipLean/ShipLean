import type { UserRef } from '@shiplean/core/api/refs';
import type { CardDto, TaskboardPersonRowDto } from '@shiplean/core/api/views';
import { passesFaces, UNASSIGNED_FACE } from '../faceFilter';

/** A person (or Unassigned) on one state's page: their tasks in that state that pass the filter. */
export interface PersonLane {
  rowId: string;
  row: TaskboardPersonRowDto;
  tasks: CardDto[];
}

/**
 * The rows of every lane of the People grouping: the view's rows in the view's order (Unassigned first; people who left the
 * project but still have tasks stay), then, on the Taskboard, `everyone` (the project's members) who have no row, by name,
 * each with no tasks. Without `everyone` (the Feature board) the view's rows.
 */
export function everyoneRows(
  rows: readonly TaskboardPersonRowDto[],
  everyone: readonly UserRef[] | null,
): TaskboardPersonRowDto[] {
  if (!everyone) return [...rows];
  const unassigned = rows.find((row) => row.person === null) ?? {
    person: null,
    taskCount: 0,
    cells: [],
  };
  const people = rows.filter((row) => row.person !== null);
  const listed = new Set(people.map((row) => row.person!.id));
  const empty = everyone
    .filter((user) => !listed.has(user.id))
    .sort((a, b) => a.name.localeCompare(b.name))
    .map((person): TaskboardPersonRowDto => ({ person, taskCount: 0, cells: [] }));
  return [unassigned, ...people, ...empty];
}

/** One state's page, grouped by People: the people the face filter keeps, each with their tasks here. */
export function phonePeopleLanes(
  rows: readonly TaskboardPersonRowDto[],
  stateId: string,
  faces: ReadonlySet<string>,
  everyone: readonly UserRef[] | null = null,
): PersonLane[] {
  return everyoneRows(rows, everyone)
    .filter((row) => faces.size === 0 || faces.has(row.person?.id ?? UNASSIGNED_FACE))
    .map((row) => ({
      rowId: row.person?.id ?? 'unassigned',
      row,
      tasks: (row.cells.find((cell) => cell.stateId === stateId)?.tasks ?? []).filter((card) =>
        passesFaces(card, faces),
      ),
    }));
}
