import type { CardDto } from '@shiplean/core/api/views';

/** The id a face stands for when it is the Unassigned face. */
export const UNASSIGNED_FACE = 'unassigned';

/** No face chosen shows every card; otherwise only the cards of the chosen people (and Unassigned). */
export function passesFaces(card: CardDto, faces: ReadonlySet<string>): boolean {
  return faces.size === 0 || faces.has(card.assignee?.id ?? UNASSIGNED_FACE);
}

/** What `faceCounts` reads: the board's story rows and people rows (the Taskboard's and the Feature board's both fit). */
export interface FaceCountView {
  rows: { cells: { tasks: CardDto[] }[] }[];
  peopleRows: { person: { id: string } | null; taskCount: number }[];
}

/**
 * Each face's task count on this board, by face id (Unassigned under `UNASSIGNED_FACE`); a face with no tasks is 0.
 * Grouped by people the people rows' own counts are used, grouped by stories the cards in the story rows are counted.
 */
export function faceCounts(
  view: FaceCountView,
  grouping: 'stories' | 'people',
): ReadonlyMap<string, number> {
  const counts = new Map<string, number>();
  if (grouping === 'people') {
    for (const row of view.peopleRows) counts.set(row.person?.id ?? UNASSIGNED_FACE, row.taskCount);
    return counts;
  }
  for (const row of view.rows) {
    for (const cell of row.cells) {
      for (const task of cell.tasks) {
        const face = task.assignee?.id ?? UNASSIGNED_FACE;
        counts.set(face, (counts.get(face) ?? 0) + 1);
      }
    }
  }
  return counts;
}
