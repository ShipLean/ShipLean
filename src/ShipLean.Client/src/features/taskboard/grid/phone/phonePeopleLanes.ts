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
 * The rows of every lane of the People grouping. The view holds only Unassigned and the people who have tasks; on the
 * Taskboard `everyone` (the project's members) adds the rest, each with no tasks: Unassigned first, then the members in
 * their order, then anyone with tasks who is no longer a member. Without `everyone` (the Feature board) the view's rows.
 */
export function everyoneRows(
  rows: readonly TaskboardPersonRowDto[],
  everyone: readonly UserRef[] | null,
): TaskboardPersonRowDto[] {
  if (!everyone) return [...rows];
  const empty = (person: UserRef | null): TaskboardPersonRowDto => ({
    person,
    taskCount: 0,
    cells: [],
  });
  const byId = new Map(rows.map((row) => [row.person?.id ?? UNASSIGNED_FACE, row]));
  const listed = new Set(everyone.map((user) => user.id));
  return [
    byId.get(UNASSIGNED_FACE) ?? empty(null),
    ...everyone.map((user) => byId.get(user.id) ?? empty(user)),
    ...rows.filter((row) => row.person && !listed.has(row.person.id)),
  ];
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
