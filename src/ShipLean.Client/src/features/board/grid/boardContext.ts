import type { WorkItemTypeId } from '../../../api/refs';
import type { Level } from '../../../routes';
import type { CardSettings } from '../cards/Card';

/** What every part of the grid needs to know about the board it draws. */
export interface BoardContext {
  projectId: string;
  projectKey: string;
  boardId: string;
  level: Level;
  compact: boolean;
  settings: CardSettings;
  /** The ids of the cards a teammate just moved (they flash). */
  flashing: ReadonlySet<string>;
  /** Expands the Done cell that holds this cursor. */
  onExpand: (cursor: string) => void;
  /** A card's roll-up of this child type was clicked: its child list opens. */
  onRollupClick?: (itemId: string, itemKey: string, type: WorkItemTypeId) => void;
}
