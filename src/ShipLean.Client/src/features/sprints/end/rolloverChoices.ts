import type { RolloverItemDto, RolloverMove } from '../../../api/sprints';

/** Where one unfinished item goes: the next sprint, the top of the backlog, or nowhere (it stays in the ended sprint). */
export type Destination = 'next' | 'backlog' | 'stay';

export type Choices = ReadonlyMap<string, Destination>;

/** The server's default for each item: the next sprint when it has one, the backlog otherwise. */
export function defaultChoices(items: RolloverItemDto[]): Choices {
  return new Map(
    items.map((item) => [
      item.workItemId,
      item.defaultTargetSprintId === null ? 'backlog' : 'next',
    ]),
  );
}

export function setAll(items: RolloverItemDto[], destination: Destination): Choices {
  return new Map(items.map((item) => [item.workItemId, destination]));
}

/** An item the viewer has not chosen for (it arrived after they started) goes where the server defaults it. */
export function destinationOf(item: RolloverItemDto, choices: Choices): Destination {
  return choices.get(item.workItemId) ?? (item.defaultTargetSprintId === null ? 'backlog' : 'next');
}

/** The points the next sprint takes on from these choices. An item with no points adds none. */
export function pointsMovingToNext(items: RolloverItemDto[], choices: Choices): number {
  return items
    .filter((item) => destinationOf(item, choices) === 'next')
    .reduce((sum, item) => sum + (item.points ?? 0), 0);
}

export interface NextSprintFigures {
  /** What the next sprint holds after the move. */
  total: number;
  /** Its team capacity, or null when nobody has entered any. */
  capacity: number | null;
  /** Points over capacity; 0 when within it or when there is no capacity to compare with. */
  over: number;
  /** Points of room left; 0 when over or when there is no capacity. */
  room: number;
}

export function nextSprintFigures(
  alreadyPlanned: number,
  capacity: number | null,
  moving: number,
): NextSprintFigures {
  const total = alreadyPlanned + moving;
  if (capacity === null) return { total, capacity, over: 0, room: 0 };
  return {
    total,
    capacity,
    over: Math.max(0, total - capacity),
    room: Math.max(0, capacity - total),
  };
}

/**
 * The moves for the choices: one per story or bug that leaves the sprint. Its open tasks of the ended sprint move with
 * it, which the server works out when it applies the moves.
 */
export function rolloverMoves(
  items: RolloverItemDto[],
  choices: Choices,
  nextSprintId: string | null,
): RolloverMove[] {
  const target = (destination: Destination) => (destination === 'next' ? nextSprintId : null);
  return items
    .map((item) => [item.workItemId, destinationOf(item, choices)] as const)
    .filter(([, destination]) => destination !== 'stay')
    .map(([workItemId, destination]) => ({ workItemId, targetSprintId: target(destination) }));
}
