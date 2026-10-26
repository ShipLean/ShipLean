import type { BoardCellDto, BoardColumnDto, BoardViewDto, CardDto } from '../../../api/views';

/** Where a card sits, or is dropped: a lane, a column, and its Doing (false) or Done (true) half. */
export interface Place {
  laneId: string;
  columnId: string;
  done: boolean;
}

/** A drop: a place, and the card it goes above (null = the end of the cell). */
export interface DropTarget extends Place {
  beforeItemId: string | null;
}

/** A card found on the board: its place, the cards of its cell in order, and its index among them. */
export interface Located extends Place {
  card: CardDto;
  cards: CardDto[];
  index: number;
}

export function locate(view: BoardViewDto, itemId: string): Located | undefined {
  for (const lane of view.lanes) {
    for (const cell of lane.cells) {
      const index = cell.cards.findIndex((card) => card.id === itemId);
      const card = cell.cards[index];
      if (card) {
        return {
          laneId: lane.id,
          columnId: cell.columnId,
          done: cell.doneHalf,
          card,
          cards: cell.cards,
          index,
        };
      }
    }
  }
  return undefined;
}

const sameCell = (a: BoardCellDto, place: Place) =>
  a.columnId === place.columnId && a.doneHalf === place.done;

function recount(column: BoardColumnDto, done: boolean, by: number): BoardColumnDto {
  return {
    ...column,
    count: column.count + by,
    doingCount: column.split && !done ? column.doingCount + by : column.doingCount,
    doneCount: column.split && done ? column.doneCount + by : column.doneCount,
  };
}

/**
 * The view with the card already where the drop put it. A board showing one lane keeps the card in it (the server
 * ignores the lane then). A drop the view cannot place (unknown lane or column, card not shown) leaves the view as it is.
 */
export function applyMove(view: BoardViewDto, itemId: string, to: DropTarget): BoardViewDto {
  const from = locate(view, itemId);
  const column = view.columns.find((candidate) => candidate.id === to.columnId);
  const targetLane = view.lanes.length === 1 ? view.lanes[0]?.id : to.laneId;
  if (!from || !column || !view.lanes.some((lane) => lane.id === targetLane)) return view;
  const state = column.states.find((mapping) => mapping.type === from.card.type.id)?.state;
  const moved: CardDto = { ...from.card, state: state ?? from.card.state };
  const target: Place = { laneId: targetLane ?? from.laneId, columnId: to.columnId, done: to.done };
  const lanes = view.lanes.map((lane) => {
    const withoutCard = lane.cells.map((cell) => ({
      ...cell,
      cards: cell.cards.filter((candidate) => candidate.id !== itemId),
    }));
    const leaving = lane.id === from.laneId;
    const entering = lane.id === target.laneId;
    if (!entering) {
      return leaving ? { ...lane, cells: withoutCard, cardCount: lane.cardCount - 1 } : lane;
    }
    const cells = withoutCard.some((cell) => sameCell(cell, target))
      ? withoutCard
      : [
          ...withoutCard,
          {
            columnId: target.columnId,
            doneHalf: target.done,
            cards: [],
            olderCount: 0,
            olderCursor: null,
          },
        ];
    return {
      ...lane,
      cardCount: lane.cardCount + (leaving ? 0 : 1),
      cells: cells.map((cell) => {
        if (!sameCell(cell, target)) return cell;
        const at = to.beforeItemId
          ? cell.cards.findIndex((candidate) => candidate.id === to.beforeItemId)
          : -1;
        const cards = [...cell.cards];
        cards.splice(at < 0 ? cards.length : at, 0, moved);
        return { ...cell, cards };
      }),
    };
  });
  const samePlace = from.columnId === target.columnId && from.done === target.done;
  return {
    ...view,
    lanes,
    columns: samePlace
      ? view.columns
      : view.columns.map((candidate) => {
          if (candidate.id === from.columnId && candidate.id === target.columnId)
            return recount(recount(candidate, from.done, -1), target.done, 1);
          if (candidate.id === from.columnId) return recount(candidate, from.done, -1);
          if (candidate.id === target.columnId) return recount(candidate, target.done, 1);
          return candidate;
        }),
  };
}

/** The view with the revision a write returned on the card. */
export function withRevision(view: BoardViewDto, itemId: string, revision: number): BoardViewDto {
  return {
    ...view,
    lanes: view.lanes.map((lane) => ({
      ...lane,
      cells: lane.cells.map((cell) => ({
        ...cell,
        cards: cell.cards.map((card) => (card.id === itemId ? { ...card, revision } : card)),
      })),
    })),
  };
}
