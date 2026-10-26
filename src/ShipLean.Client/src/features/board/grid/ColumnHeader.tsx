import './board.css';
import { boardTestIds as ids } from '../testIds';
import type { BoardColumnDto } from '../../../api/views';
import { DodButton } from '../dod/DodButton';
import { mappedStates } from './layout';

// A column's header: name, cards against the WIP limit, and how many a filter hides (over it the header turns danger and nothing is blocked; hidden
// cards still count), the states it maps, and for a split column its Doing and Done halves. `flashes` counts the board
// settings changes teammates made: each one flashes the header again.
export function ColumnHeader({ column, flashes }: { column: BoardColumnDto; flashes: number }) {
  const mapped = mappedStates(column);
  return (
    <div
      data-testid={ids.columnHeader}
      data-column={column.id}
      data-over={column.overLimit}
      data-flashes={flashes}
      className="board-header relative grid min-w-0 content-start gap-0.5 rounded-control border border-edge border-t-2 py-[5px] pr-1.5 pl-2"
      style={
        column.overLimit
          ? {
              borderTopColor: 'var(--color-danger)',
              background: 'color-mix(in srgb, var(--color-danger) 6%, var(--color-surface))',
            }
          : { background: 'var(--color-surface)' }
      }
    >
      {flashes > 0 ? (
        // A new count remounts only this ring, which restarts the animation and leaves the header's focus alone.
        <span
          key={flashes}
          aria-hidden="true"
          className="board-header-flash pointer-events-none absolute -inset-px rounded-control"
        />
      ) : null}
      <div className="flex h-5 min-w-0 items-center gap-1.5">
        <span
          data-testid={ids.columnName}
          className="truncate text-[length:var(--text-sm)] font-semibold"
        >
          {column.name}
        </span>
        <span
          data-testid={ids.columnCount}
          className={`text-[length:var(--text-xs)] font-medium whitespace-nowrap tabular-nums ${
            column.overLimit
              ? 'inline-flex h-[18px] items-center rounded-full px-1.5 font-semibold text-alert'
              : 'text-ink-muted'
          }`}
          style={
            column.overLimit
              ? { background: 'color-mix(in srgb, var(--color-danger) 18%, transparent)' }
              : undefined
          }
        >
          {column.wipLimit === null ? column.count : `${column.count}/${column.wipLimit}`}
        </span>
        {column.hiddenCount > 0 ? (
          <span
            data-testid={ids.columnHidden}
            className="text-[length:var(--text-2xs)] whitespace-nowrap text-ink-muted tabular-nums"
          >
            +{column.hiddenCount} hidden
          </span>
        ) : null}
        <DodButton column={column} />
      </div>
      <div
        data-testid={ids.columnMap}
        className="h-4 truncate text-[length:var(--text-2xs)] leading-4 text-ink-muted"
      >
        {mapped}
      </div>
      {column.split ? (
        <div className="grid h-4 grid-cols-2 gap-x-2 text-[length:var(--text-2xs)] leading-4 text-ink-muted">
          <span data-testid={ids.subDoing}>
            Doing <b className="font-semibold text-ink tabular-nums">{column.doingCount}</b>
          </span>
          <span data-testid={ids.subDone} className="border-l border-edge pl-2">
            Done <b className="font-semibold text-ink tabular-nums">{column.doneCount}</b>
          </span>
        </div>
      ) : null}
    </div>
  );
}
