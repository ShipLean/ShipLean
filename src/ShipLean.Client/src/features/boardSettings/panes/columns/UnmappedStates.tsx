import type { BoardTypeStatesDto } from '../../../../api/boards';
import type { BoardColumnJson } from '../../../../api/persisted';
import { boardSettingsTestIds as ids } from '../../testIds';
import { fixFor, typeNames, type Fix, type UnmappedState } from './columnEdits';

function fixLabel(fix: Fix) {
  const type = typeNames[fix.unmapped.type].toLowerCase();
  return fix.kind === 'remap'
    ? `Map ${fix.column.name} to ${fix.unmapped.state.name} for ${type}s`
    : `+ Add a column for ${fix.unmapped.state.name}`;
}

/** A warning per state no column maps, each with the one-click fix; or the all-clear. */
export function UnmappedStates({
  columns,
  states,
  unmapped,
  readOnly,
  onFix,
}: {
  columns: BoardColumnJson[];
  states: BoardTypeStatesDto[];
  unmapped: UnmappedState[];
  readOnly: boolean;
  onFix(fix: Fix): void;
}) {
  if (unmapped.length === 0) {
    return (
      <div
        data-testid={ids.columnsAllMapped}
        className="rounded-panel border border-edge border-l-[3px] border-l-good bg-panel px-3 py-2 text-[length:var(--text-sm)]"
      >
        Every state of every type is on a column.
      </div>
    );
  }
  return (
    <>
      {unmapped.map((item) => {
        const fix = fixFor(columns, states, item);
        const type = typeNames[item.type];
        return (
          <div
            key={item.state.id}
            data-testid={ids.columnsUnmapped}
            className="flex items-center justify-between gap-3 rounded-panel border border-edge border-l-[3px] border-l-caution bg-panel px-3 py-2 text-[length:var(--text-sm)]"
          >
            <div className="grid gap-0.5">
              <b>
                {type} state {item.state.name} isn&apos;t on any column: cards in it would disappear
                from the board.
              </b>
              <span className="text-ink-muted">
                Pick it for a column in the {type} column below.
              </span>
            </div>
            <button
              type="button"
              data-testid={ids.columnsFix}
              disabled={readOnly}
              className="h-7 flex-none rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] disabled:opacity-50"
              onClick={() => onFix(fix)}
            >
              {fixLabel(fix)}
            </button>
          </div>
        );
      })}
    </>
  );
}
