import type { CardDto } from '../../../api/views';

/** The id a face stands for when it is the Unassigned face. */
export const UNASSIGNED_FACE = 'unassigned';

/** No face chosen shows every card; otherwise only the cards of the chosen people (and Unassigned). */
export function passesFaces(card: CardDto, faces: ReadonlySet<string>): boolean {
  return faces.size === 0 || faces.has(card.assignee?.id ?? UNASSIGNED_FACE);
}
