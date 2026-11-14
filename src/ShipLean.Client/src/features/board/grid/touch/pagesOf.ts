import type {
  BoardCellDto,
  BoardColumnDto,
  BoardLaneDto,
  BoardViewDto,
} from '@shiplean/core/api/views';
import { cellOf, newCardLaneIndex, tracksOf, type Track } from '../layout';

/** One lane's cell in a column page: a column has one, a split column its Doing then its Done. */
export interface PageCell {
  track: Track;
  cell: BoardCellDto | undefined;
  /** Cards in the cell, with the older ones Show older would load. */
  count: number;
  offersNew: boolean;
}

/** One lane inside a column page. A collapsed lane keeps its header and has no cells. */
export interface PageLane {
  lane: BoardLaneDto;
  collapsed: boolean;
  /** Cards of the lane in this column. */
  count: number;
  cells: PageCell[];
}

/** One column of the phone board: a page of the pager. */
export interface ColumnPageData {
  column: BoardColumnDto;
  lanes: PageLane[];
}

const sizeOf = (cell: BoardCellDto | undefined) => (cell ? cell.cards.length + cell.olderCount : 0);

/**
 * The phone board's pages, one per column. A lane with no cards in a column is left out of that page (the lane that
 * holds "+ New" stays in the first column), a collapsed lane keeps its header, and a split column is one page with its
 * Doing cell, then its Done cell.
 */
export function pagesOf(view: BoardViewDto, collapsed: ReadonlySet<string>): ColumnPageData[] {
  const tracks = tracksOf(view.columns);
  const newLane = view.lanes[newCardLaneIndex(view.lanes)]?.id;
  return view.columns.map((column) => {
    const own = tracks.filter((track) => track.column.id === column.id);
    const lanes = view.lanes.flatMap((lane): PageLane[] => {
      const cells = own.map((track) => {
        const cell = cellOf(lane, track);
        return {
          track,
          cell,
          count: sizeOf(cell),
          offersNew: lane.id === newLane && track.first && !track.doneHalf,
        };
      });
      const count = cells.reduce((sum, c) => sum + c.count, 0);
      if (count === 0 && !cells.some((c) => c.offersNew)) return [];
      const folded = collapsed.has(lane.id);
      return [{ lane, collapsed: folded, count, cells: folded ? [] : cells }];
    });
    return { column, lanes };
  });
}
