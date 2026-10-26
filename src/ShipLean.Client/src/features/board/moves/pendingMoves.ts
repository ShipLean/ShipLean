// The moves of each board that are sent or waiting to be: while any is, a teammate's event must not refetch the board
// over the card positions shown. The last move to settle refetches.
const pending = new Map<string, number>();

export const movesPending = (boardId: string): boolean => (pending.get(boardId) ?? 0) > 0;

export function moveStarted(boardId: string) {
  pending.set(boardId, (pending.get(boardId) ?? 0) + 1);
}

/** Returns true when it was the last move pending. */
export function moveSettled(boardId: string): boolean {
  const left = (pending.get(boardId) ?? 1) - 1;
  if (left <= 0) pending.delete(boardId);
  else pending.set(boardId, left);
  return left <= 0;
}
