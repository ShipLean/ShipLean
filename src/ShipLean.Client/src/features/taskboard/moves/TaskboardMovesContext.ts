import { createContext, useContext } from 'react';
import type { UserRef } from '@shiplean/core/api/refs';
import type { CardDto } from '@shiplean/core/api/views';
import type { MoveMethod, TaskboardGrouping } from '../taskboardApi';
import type { TaskboardGridView } from '../taskboardGridView';
import type { DropTarget } from './moveRequests';

/** What a card and a cell need from the board to move cards: set up once by `TaskboardDnd`. */
export interface TaskboardMoves {
  /** Pick up, or null when there is no one to assign (not signed in) or no In Progress column. */
  pickUp: ((card: CardDto, rowId: string) => void) | null;
  /** Alt + an arrow key on a focused card. True when the key was a move (the card is then given focus again once it lands). */
  keyMove: (card: CardDto, rowId: string, key: string) => boolean;
  /** True once, for the card a keyboard move just put down: it takes focus back. */
  takeFocus: (cardKey: string) => boolean;
  /** Every caller names how the user moved the card. */
  drop: (card: CardDto, fromRowId: string, to: DropTarget, method: MoveMethod) => void;
  /** Moves a task to another story (null = Unparented), keeping its state. */
  reparent: (card: CardDto, fromRowId: string, storyId: string | null, method: MoveMethod) => void;
  /** Gives a task to a person (null = Unassigned), keeping its state. */
  assign: (card: CardDto, fromRowId: string, person: UserRef | null, method: MoveMethod) => void;
  /** The board's states, stories and people, for the task actions. */
  view: TaskboardGridView;
  grouping: TaskboardGrouping;
}

export const TaskboardMovesContext = createContext<TaskboardMoves | null>(null);

export function useTaskboardMoves(): TaskboardMoves {
  const moves = useContext(TaskboardMovesContext);
  if (!moves) throw new Error('A Taskboard card must sit inside TaskboardDnd.');
  return moves;
}
