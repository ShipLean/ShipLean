import type { BoardViewDto, CardDto } from '../../../../api/views';
import type { Level } from '../../../../routes';
import { openText } from './openText';
import { locate, type DropTarget } from '../applyMove';

/** The card a drop would put into Done while children below it are still open, with what is open by type. */
export interface OpenBelow {
  card: CardDto;
  open: number;
  /** "2 stories and 1 bug". */
  text: string;
}

/**
 * Set when the drop takes a card of a Features or Epics board into the board's last column from another one while its
 * roll-ups still count open children; null for every other drop. Stories keep their tasks out of the question.
 */
export function openBelow(
  view: BoardViewDto,
  level: Level,
  itemId: string,
  to: DropTarget,
): OpenBelow | null {
  if (level === 'stories') return null;
  const last = view.columns[view.columns.length - 1];
  const at = locate(view, itemId);
  if (!last || !at || to.columnId !== last.id || at.columnId === last.id) return null;
  const open = at.card.rollups.reduce((sum, rollup) => sum + rollup.open, 0);
  return open > 0 ? { card: at.card, open, text: openText(at.card.rollups, level) } : null;
}
