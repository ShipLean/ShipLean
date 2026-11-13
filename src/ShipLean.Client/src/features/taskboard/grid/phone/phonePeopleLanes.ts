import type { CardDto, TaskboardPersonRowDto } from '@shiplean/core/api/views';
import { passesFaces, UNASSIGNED_FACE } from '../faceFilter';

/** A person (or Unassigned) on one state's page: their tasks in that state that pass the filter. */
export interface PersonLane {
  rowId: string;
  row: TaskboardPersonRowDto;
  tasks: CardDto[];
}

/** One state's page, grouped by People: the people the face filter keeps, in the view's order, each with their tasks here. */
export function phonePeopleLanes(
  rows: readonly TaskboardPersonRowDto[],
  stateId: string,
  faces: ReadonlySet<string>,
): PersonLane[] {
  return rows
    .filter((row) => faces.size === 0 || faces.has(row.person?.id ?? UNASSIGNED_FACE))
    .map((row) => ({
      rowId: row.person?.id ?? 'unassigned',
      row,
      tasks: (row.cells.find((cell) => cell.stateId === stateId)?.tasks ?? []).filter((card) =>
        passesFaces(card, faces),
      ),
    }));
}
