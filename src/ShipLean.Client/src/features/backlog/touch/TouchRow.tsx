import type { MouseEvent, ReactNode } from 'react';
import type { BacklogRowDto } from '../../../api/views';
import { Avatar } from '../../../components/Avatar';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { useItemPeek } from '../../itemPeek/useItemPeek';
import { ParentCell } from '../list/ParentCell';
import { SprintCell } from '../list/SprintCell';
import { useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';
import { useTouchSelectMode } from './selectMode';

// Taps on these keep their own behaviour instead of opening or selecting the row.
const inside = 'a, button, input, select, textarea, label';

/**
 * One backlog item as a two-line touch row (900px and below, SHIP-716 D1). List, Planning and Tree all render it.
 *
 * Props, the surface the other modes build on:
 * - `row`: the item.
 * - `projectKey`: the open project's key.
 * - `handle?`: what the left-edge slot (full row height) shows. Left out, the slot shows the rank. TouchRowList passes
 *   the select box while selecting, or its `renderHandle` (the drag handle) when handles are on.
 * - `forecast?`: the sprint name Forecast puts the item in, shown as "→ <forecast>" on the meta line. Planning passes it.
 * - `trailing?`: shown last on the right, after points and the assignee (the row's ⋯).
 * - `flash`: a teammate just changed the item; the row flashes.
 *
 * The row's test id is `backlogTestIds.row`, as in every mode. Every row carries `data-drop-target` with its id, so a
 * drag can find the row under the finger. Tapping the row opens the item modal, or in Select mode toggles the row; taps
 * on controls inside it (links, buttons, inputs, labels) keep their own. The points here are display only, with a warn
 * ? when there's no estimate.
 */
export function TouchRow({
  row,
  projectKey,
  handle,
  forecast,
  trailing,
  flash,
}: {
  row: BacklogRowDto;
  projectKey: string;
  handle?: ReactNode;
  forecast?: string;
  trailing?: ReactNode;
  flash: boolean;
}) {
  const peek = useItemPeek();
  const { selecting } = useTouchSelectMode();
  const selection = useSelection();
  const selected = selection.has(row.id);
  const closed = row.state?.category === 'completed';
  const tap = (event: MouseEvent<HTMLDivElement>) => {
    if (event.target instanceof Element && event.target.closest(inside) !== null) return;
    if (selecting) selection.toggle(row.id);
    else peek.open(row.key);
  };
  return (
    <div
      role="button"
      tabIndex={0}
      aria-label={`${row.key} ${row.title}`}
      data-testid={ids.row}
      data-project-key={projectKey}
      data-key={row.key}
      data-row-id={row.id}
      data-drop-target={row.id}
      data-flash={flash}
      data-selected={selected}
      onClick={tap}
      onKeyDown={(event) => {
        if (event.key === 'Enter' && event.target === event.currentTarget && !selecting)
          peek.open(row.key);
      }}
      className={`flex min-h-[var(--touch-target)] items-stretch border-b border-edge text-[length:var(--text-sm)] transition-colors duration-700 data-[selected=true]:bg-accent-soft ${flash ? 'bg-signal-soft' : ''} ${closed ? 'text-ink-muted' : 'text-ink'}`}
    >
      <div
        data-testid={ids.touchRowHandle}
        className="flex w-9 flex-none items-center justify-center self-stretch text-[length:var(--text-xs)] text-ink-muted tabular-nums"
      >
        {handle ?? <span data-testid={ids.rank}>{row.rank}</span>}
      </div>
      <div className="min-w-0 flex-1 py-2 pr-2">
        <div className="flex items-start gap-1.5">
          {row.type ? (
            <span className="mt-0.5 flex-none">
              <TypeIcon type={row.type.id} />
            </span>
          ) : null}
          <span data-testid={ids.title} className="line-clamp-2 min-w-0 font-medium break-words">
            {row.title}
          </span>
        </div>
        <div
          data-testid={ids.touchRowMeta}
          className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-[length:var(--text-xs)] text-ink-muted"
        >
          <span data-testid={ids.key}>{row.key}</span>
          {row.state ? (
            <span data-testid={ids.state}>
              <StateDot category={row.state.category} name={row.state.name} />
            </span>
          ) : null}
          {row.sprint ? <SprintCell sprint={row.sprint} /> : null}
          {forecast === undefined ? null : (
            <span data-testid={ids.touchRowForecast}>→ {forecast}</span>
          )}
          {row.parent ? (
            <span className="max-w-full min-w-0 break-words">
              <ParentCell parent={row.parent} />
            </span>
          ) : null}
        </div>
      </div>
      <div className="flex flex-none items-center gap-2 pr-3">
        {row.points === null ? (
          <span
            data-testid={ids.points}
            title="Not estimated"
            className="font-semibold text-caution"
          >
            ?
          </span>
        ) : (
          <span data-testid={ids.points} className="font-semibold tabular-nums">
            {row.points}
          </span>
        )}
        <span data-testid={ids.assignee}>
          <Avatar {...(row.assignee ?? {})} />
        </span>
        {trailing}
      </div>
    </div>
  );
}
