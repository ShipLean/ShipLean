import { boardRootKey } from '../boardApi';
import { useStringPreference } from './useStringPreference';
import { useQueryClient } from '@tanstack/react-query';

// Per-user, per-board view choices, saved as preferences (a JSON string each).
//   view:board-lanes:{boardId}  "on" | "off" on Stories. On Features, "on" (stored swimlanes; also the unset default),
//                               "none" | "off" (one lane) or "epic" (one lane per Epic). The server reads it when it
//                               builds the board, so a change refetches the board.
//   view:board-cards:{boardId}  "full" | "compact". Only the client reads it.
export const lanesPreferenceName = (boardId: string) => `view:board-lanes:${boardId}`;
export const cardsPreferenceName = (boardId: string) => `view:board-cards:${boardId}`;

/** The lane choice of a board; saving it refetches the board once it is stored. */
export function useBoardLanes(projectId: string, boardId: string) {
  const queryClient = useQueryClient();
  const preference = useStringPreference(lanesPreferenceName(boardId));
  return {
    value: preference.value,
    choose: (value: string) =>
      preference.save(value, () => {
        void queryClient.invalidateQueries({ queryKey: boardRootKey(projectId, boardId) });
      }),
  };
}

/** Full or compact cards, per user and board. */
export function useCardDensity(boardId: string) {
  const preference = useStringPreference(cardsPreferenceName(boardId));
  return {
    compact: preference.value === 'compact',
    setCompact: (compact: boolean) => preference.save(compact ? 'compact' : 'full'),
  };
}
