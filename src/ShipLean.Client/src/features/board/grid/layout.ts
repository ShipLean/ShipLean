import type { BoardCellDto, BoardColumnDto, BoardLaneDto } from '../../../api/views';

/** One track of the grid: a column, or one half of a split column. */
export interface Track {
  column: BoardColumnDto;
  /** False for the Doing half of a split column and for a column that is not split. */
  doneHalf: boolean;
  /** The column is the board's last one (Done). */
  last: boolean;
  first: boolean;
}

/** The grid's tracks in order: a split column has a Doing and a Done track. */
export function tracksOf(columns: BoardColumnDto[]): Track[] {
  return columns.flatMap((column, index) => {
    const base = { column, last: index === columns.length - 1, first: index === 0 };
    return column.split
      ? [
          { ...base, doneHalf: false },
          { ...base, doneHalf: true },
        ]
      : [{ ...base, doneHalf: false }];
  });
}

export const gridColumns = (tracks: Track[]) => `repeat(${tracks.length}, minmax(0, 1fr))`;

/** The lane's cell of one track; a lane the server sent without that cell has none. */
export function cellOf(lane: BoardLaneDto, track: Track): BoardCellDto | undefined {
  return lane.cells.find(
    (cell) => cell.columnId === track.column.id && cell.doneHalf === track.doneHalf,
  );
}

/** A board that shows one default lane has no lane header and nothing to collapse. */
export const showsLaneHeaders = (lanes: BoardLaneDto[]) =>
  !(lanes.length === 1 && lanes[0]?.kind === 'default');

/** The state names a column maps, once each, for its header's second line. */
export function mappedStates(column: BoardColumnDto): string {
  return [...new Set(column.states.map((mapping) => mapping.state.name))].join(' · ');
}

/** The lane that offers "+ New": the one for items no criteria or epic caught, else the first lane. */
export function newCardLaneIndex(lanes: BoardLaneDto[]): number {
  const index = lanes.findIndex((lane) => lane.kind === 'default' || lane.kind === 'noEpic');
  return index === -1 ? 0 : index;
}
