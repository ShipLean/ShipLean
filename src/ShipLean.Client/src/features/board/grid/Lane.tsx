import { useState } from 'react';
import type { BoardLaneDto } from '../../../api/views';
import { swatchVar } from '../cards/cardStyle';
import { boardTestIds as ids } from '../testIds';
import type { BoardContext } from './boardContext';
import { Cell } from './Cell';
import { cellOf, gridColumns, type Track } from './layout';

// One swimlane: a header (collapse, name, criteria, card count, open points) over a row of cells. A board showing
// one default lane has no header, so nothing to collapse.
export function Lane({
  lane,
  tracks,
  context,
  withHeader,
  offersNew,
}: {
  lane: BoardLaneDto;
  tracks: Track[];
  context: BoardContext;
  withHeader: boolean;
  /** This is the lane that holds "+ New" (in its first cell). */
  offersNew: boolean;
}) {
  const [collapsed, setCollapsed] = useState(false);
  const folded = withHeader && collapsed;
  return (
    <section
      data-testid={ids.lane}
      data-lane={lane.id}
      data-kind={lane.kind}
      data-collapsed={folded}
      className="grid gap-1"
    >
      {withHeader ? (
        <div
          data-testid={ids.laneHeader}
          className="flex h-7 items-center gap-2 px-1 text-[length:var(--text-sm)]"
        >
          <button
            type="button"
            data-testid={ids.laneToggle}
            aria-expanded={!collapsed}
            aria-label={`${collapsed ? 'Expand' : 'Collapse'} ${lane.name}`}
            className="btn-ghost size-5 p-0"
            onClick={() => setCollapsed(!collapsed)}
          >
            {collapsed ? '▶' : '▼'}
          </button>
          <span
            aria-hidden
            className="h-4 w-[3px] rounded-sm"
            style={{ background: lane.colour ? swatchVar(lane.colour) : 'var(--color-border)' }}
          />
          <b data-testid={ids.laneName} className="font-semibold">
            {lane.name}
          </b>
          {lane.criteriaText === null ? null : (
            <span data-testid={ids.laneCriteria} className="text-ink-muted">
              {lane.criteriaText}
            </span>
          )}
          <span data-testid={ids.laneCount} className="text-ink-muted tabular-nums">
            {lane.cardCount} {lane.cardCount === 1 ? 'card' : 'cards'}
          </span>
          <span data-testid={ids.lanePoints} className="text-ink-muted tabular-nums">
            {lane.openPoints} pts open
          </span>
        </div>
      ) : null}
      {folded ? null : (
        <div className="grid gap-x-2" style={{ gridTemplateColumns: gridColumns(tracks) }}>
          {tracks.map((track) => (
            <Cell
              key={`${track.column.id}:${track.doneHalf}`}
              cell={cellOf(lane, track)}
              track={track}
              laneId={lane.id}
              context={context}
              offersNew={offersNew && track.first && !track.doneHalf}
            />
          ))}
        </div>
      )}
    </section>
  );
}
