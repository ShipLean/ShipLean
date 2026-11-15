import { boardTestIds as ids } from '../../testIds';
import type { BoardContext } from '../boardContext';
import { ColumnHeader } from '../ColumnHeader';
import { ColumnLane } from './ColumnLane';
import type { ColumnPageData } from './pagesOf';

// One page of the phone board: the column's header, which scrolls with the page, then its lanes.
export function ColumnPage({
  data,
  context,
  headerFlashes,
  onToggle,
}: {
  data: ColumnPageData;
  context: BoardContext;
  headerFlashes: number;
  onToggle: (laneId: string) => void;
}) {
  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] content-start gap-2 pt-1">
      <ColumnHeader column={data.column} flashes={headerFlashes} />
      {data.lanes.length === 0 ? (
        <p
          data-testid={ids.columnEmpty}
          className="py-1.5 text-center text-[length:var(--text-xs)] text-ink-muted"
        >
          No cards
        </p>
      ) : null}
      {data.lanes.map((entry) => (
        <ColumnLane
          key={entry.lane.id}
          entry={entry}
          split={data.column.split}
          context={context}
          onToggle={() => onToggle(entry.lane.id)}
        />
      ))}
    </div>
  );
}
