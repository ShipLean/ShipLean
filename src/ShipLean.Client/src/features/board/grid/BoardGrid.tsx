import type { BoardViewDto } from '../../../api/views';
import { boardTestIds as ids } from '../testIds';
import type { BoardContext } from './boardContext';
import { ColumnHeader } from './ColumnHeader';
import { BoardDnd } from '../moves/BoardDnd';
import { gridColumns, newCardLaneIndex, showsLaneHeaders, tracksOf } from './layout';
import { Lane } from './Lane';

// The grid: a sticky row of column headers over the lanes. One template shared by the header and every lane keeps the
// columns lined up; a split column spans its Doing and Done tracks.
export function BoardGrid({
  view,
  context,
  headerFlashes,
}: {
  view: BoardViewDto;
  context: BoardContext;
  /** Board settings changes by teammates since the board opened. */
  headerFlashes: number;
}) {
  const tracks = tracksOf(view.columns);
  const withHeaders = showsLaneHeaders(view.lanes);
  const newLane = newCardLaneIndex(view.lanes);
  return (
    <BoardDnd view={view} context={context}>
      <div data-testid={ids.grid} className="min-w-[1120px] pb-4">
        <div
          className="sticky top-0 z-[4] grid gap-x-2 bg-canvas pt-2 pb-1.5"
          style={{ gridTemplateColumns: gridColumns(tracks) }}
        >
          {view.columns.map((column) => (
            <div key={column.id} style={{ gridColumn: column.split ? 'span 2' : undefined }}>
              <ColumnHeader column={column} flashes={headerFlashes} />
            </div>
          ))}
        </div>
        <div className="grid gap-2">
          {view.lanes.map((lane, index) => (
            <Lane
              key={lane.id}
              lane={lane}
              tracks={tracks}
              context={context}
              withHeader={withHeaders}
              offersNew={index === newLane}
            />
          ))}
        </div>
      </div>
    </BoardDnd>
  );
}
