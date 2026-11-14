import { swatchVar } from '../../cards/cardStyle';
import { boardTestIds as ids } from '../../testIds';
import type { BoardContext } from '../boardContext';
import { Cell } from '../Cell';
import type { PageLane } from './pagesOf';

// One lane inside a column page: a short header (▼, name, its count in this column), then its cell, or for a split
// column its Doing cell and its Done cell under a small heading each. The collapsed set lives in BoardPages.
export function ColumnLane({
  entry,
  split,
  context,
  onToggle,
}: {
  entry: PageLane;
  split: boolean;
  context: BoardContext;
  onToggle: () => void;
}) {
  const { lane, collapsed } = entry;
  return (
    <section
      data-testid={ids.columnLane}
      data-lane={lane.id}
      data-collapsed={collapsed}
      className="grid gap-1.5"
    >
      {entry.header ? (
        <div
          data-testid={ids.columnLaneHeader}
          className="flex min-h-(--touch-target) items-center gap-2 text-[length:var(--text-sm)]"
        >
          <button
            type="button"
            data-testid={ids.columnLaneToggle}
            aria-expanded={!collapsed}
            aria-label={`${collapsed ? 'Expand' : 'Collapse'} ${lane.name}`}
            className="btn-ghost -ml-1.5 size-(--touch-target) shrink-0 p-0"
            onClick={onToggle}
          >
            {collapsed ? '▶' : '▼'}
          </button>
          <span
            aria-hidden
            className="h-4 w-[3px] shrink-0 rounded-sm"
            style={{ background: lane.colour ? swatchVar(lane.colour) : 'var(--color-border)' }}
          />
          <b data-testid={ids.columnLaneName} className="min-w-0 truncate font-semibold">
            {lane.name}
          </b>
          <span
            data-testid={ids.columnLaneCount}
            className="ml-auto shrink-0 text-ink-muted tabular-nums"
          >
            {collapsed ? 'collapsed' : `${entry.count} ${entry.count === 1 ? 'card' : 'cards'}`}
          </span>
        </div>
      ) : null}
      {entry.cells.map(({ track, cell, count, offersNew }) => (
        <div key={`${track.column.id}:${track.doneHalf}`} className="grid gap-1">
          {split ? (
            <div
              data-testid={ids.columnLaneSub}
              className="flex items-baseline gap-1.5 px-0.5 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase"
            >
              {track.doneHalf ? 'Done' : 'Doing'}
              <b className="text-ink tabular-nums">{count}</b>
            </div>
          ) : null}
          <Cell
            cell={cell}
            track={track}
            laneId={lane.id}
            context={context}
            offersNew={offersNew}
          />
        </div>
      ))}
    </section>
  );
}
