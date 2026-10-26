import type { KeyboardEvent } from 'react';
import type { BoardTypeStatesDto } from '../../../../api/boards';
import type { BoardColumnJson } from '../../../../api/persisted';
import { boardSettingsTestIds as ids } from '../../testIds';
import { liveStates, parseWipLimit, setState, stateOf } from './columnEdits';
import { DefinitionOfDonePreview } from './DefinitionOfDonePreview';
import { StateSelect } from './StateSelect';

const iconButton =
  'inline-grid size-6 place-items-center rounded-control text-ink-muted hover:bg-sunken disabled:cursor-default disabled:opacity-35 disabled:hover:bg-transparent';

/** One column of the board: its name, WIP limit, split, state per type and a toggle for its definition of done. */
export function ColumnRow({
  column,
  states,
  fixed,
  first,
  last,
  readOnly,
  dodOpen,
  onChange,
  onToggleDod,
  onDelete,
  onMove,
  onDragStart,
  onDrop,
}: {
  column: BoardColumnJson;
  states: BoardTypeStatesDto[];
  fixed: boolean;
  first: boolean;
  last: boolean;
  readOnly: boolean;
  dodOpen: boolean;
  onChange(next: BoardColumnJson): void;
  onToggleDod(): void;
  onDelete(): void;
  /** -1 up, 1 down. */
  onMove(step: number): void;
  onDragStart(): void;
  onDrop(): void;
}) {
  const keyDown = (event: KeyboardEvent) => {
    if (!event.altKey || fixed) return;
    if (event.key === 'ArrowUp') onMove(-1);
    else if (event.key === 'ArrowDown') onMove(1);
    else return;
    event.preventDefault();
  };
  const lines = column.definitionOfDone.split('\n').filter((line) => line.trim() !== '').length;
  return (
    <>
      <tr
        data-testid={ids.columnRow}
        className="border-b border-edge"
        onDragOver={(event) => {
          if (!fixed) event.preventDefault();
        }}
        onDrop={() => {
          if (!fixed) onDrop();
        }}
      >
        <td className="w-6 pl-1">
          <button
            type="button"
            data-testid={ids.columnGrip}
            draggable={!fixed && !readOnly}
            disabled={fixed || readOnly}
            aria-label={`Move column ${column.name} (Alt+Up or Alt+Down)`}
            title={
              fixed
                ? 'The first and last columns stay in place'
                : 'Drag, or press Alt+Up or Alt+Down'
            }
            className={`${iconButton} cursor-grab`}
            onKeyDown={keyDown}
            onDragStart={onDragStart}
          >
            ⠿
          </button>
        </td>
        <td className="py-1 pr-2">
          <div className="flex items-center gap-1.5">
            <input
              data-testid={ids.columnName}
              className="field h-[26px] min-w-20 flex-1"
              aria-label="Column name"
              value={column.name}
              readOnly={readOnly}
              onChange={(event) => onChange({ ...column, name: event.target.value })}
            />
            {first ? (
              <span className="text-[length:var(--text-xs)] text-ink-muted">First</span>
            ) : null}
            {last ? (
              <span className="text-[length:var(--text-xs)] text-ink-muted">Last</span>
            ) : null}
          </div>
        </td>
        <td className="pr-2">
          {fixed ? (
            <span className="text-ink-muted" title="The first and last columns have no WIP limit">
              –
            </span>
          ) : (
            <input
              data-testid={ids.columnWip}
              type="number"
              min={1}
              className="field h-[26px] w-14 text-right"
              placeholder="None"
              aria-label={`WIP limit for ${column.name}`}
              value={column.wipLimit ?? ''}
              readOnly={readOnly}
              onChange={(event) =>
                onChange({ ...column, wipLimit: parseWipLimit(event.target.value) })
              }
            />
          )}
        </td>
        <td className="pr-2">
          {fixed ? (
            <span className="text-ink-muted" title="The first and last columns can't be split">
              –
            </span>
          ) : (
            <label className="inline-flex items-center gap-1.5 text-[length:var(--text-sm)]">
              <input
                data-testid={ids.columnSplit}
                type="checkbox"
                checked={column.split}
                disabled={readOnly}
                onChange={(event) => onChange({ ...column, split: event.target.checked })}
              />
              Doing / Done
            </label>
          )}
        </td>
        {states.map(({ type }) => (
          <td key={type} className="pr-2">
            <StateSelect
              type={type}
              columnName={column.name}
              states={liveStates(states, type)}
              value={stateOf(column, type)}
              disabled={readOnly}
              onChange={(stateId) => onChange(setState(column, type, stateId))}
            />
          </td>
        ))}
        <td className="pr-2">
          <button
            type="button"
            data-testid={ids.columnDodToggle}
            aria-expanded={dodOpen}
            className="h-6 rounded-control px-1.5 text-[length:var(--text-sm)] hover:bg-sunken"
            onClick={onToggleDod}
          >
            {lines === 0 ? 'Add' : `${lines} ${lines === 1 ? 'item' : 'items'}`}{' '}
            <span className="text-ink-muted">{dodOpen ? '▴' : '▾'}</span>
          </button>
        </td>
        <td className="pr-1">
          <button
            type="button"
            data-testid={ids.columnDelete}
            disabled={fixed || readOnly}
            aria-label={`Delete column ${column.name}`}
            title={fixed ? "The first and last columns can't be deleted" : 'Delete column'}
            className={iconButton}
            onClick={onDelete}
          >
            ✕
          </button>
        </td>
      </tr>
      {dodOpen ? (
        <tr className="border-b border-edge bg-canvas">
          <td />
          <td colSpan={5 + states.length}>
            <div className="grid grid-cols-[minmax(0,1fr)_240px] gap-3 py-2">
              <label className="grid gap-1 text-[length:var(--text-xs)] text-ink-muted">
                Definition of done for {column.name} · markdown
                <textarea
                  data-testid={ids.columnDodText}
                  className="field w-full font-mono text-[length:var(--text-sm)]"
                  rows={4}
                  placeholder="- What has to be true before a card leaves this column"
                  value={column.definitionOfDone}
                  readOnly={readOnly}
                  onChange={(event) =>
                    onChange({ ...column, definitionOfDone: event.target.value })
                  }
                />
              </label>
              <DefinitionOfDonePreview name={column.name} markdown={column.definitionOfDone} />
            </div>
          </td>
        </tr>
      ) : null}
    </>
  );
}
