import { useState } from 'react';
import type { BoardTypeStatesDto } from '../../../../api/boards';
import type { BoardColumnJson } from '../../../../api/persisted';
import { TypeIcon } from '../../../../components/TypeIcon';
import { boardSettingsTestIds as ids } from '../../testIds';
import {
  blankColumn,
  insertBeforeLast,
  liveStates,
  moveColumn,
  replaceColumn,
  typeNames,
} from './columnEdits';
import { ColumnRow } from './ColumnRow';

/** The column table with its Add column footer; the first and last columns are fixed. */
export function ColumnsTable({
  columns,
  states,
  readOnly,
  onChange,
  onDelete,
}: {
  columns: BoardColumnJson[];
  states: BoardTypeStatesDto[];
  readOnly: boolean;
  onChange(next: BoardColumnJson[]): void;
  onDelete(column: BoardColumnJson, index: number): void;
}) {
  const [dodOpenId, setDodOpenId] = useState<string>();
  const [dragFrom, setDragFrom] = useState<number>();
  const last = columns.length - 1;
  const mappedCount = (type: BoardTypeStatesDto['type']) => {
    const live = liveStates(states, type);
    const mapped = new Set(columns.flatMap((c) => c.stateMappings.map((m) => m.stateId)));
    return [live.filter((s) => mapped.has(s.id)).length, live.length] as const;
  };

  return (
    <div className="overflow-hidden rounded-panel border border-edge bg-panel">
      <table className="w-full border-collapse text-[length:var(--text-sm)]">
        <thead>
          <tr className="border-b border-edge bg-panel text-left text-[length:var(--text-xs)] text-ink-muted">
            <th className="w-6" />
            <th className="py-1.5 font-medium">Column</th>
            <th className="w-[72px] font-medium">WIP limit</th>
            <th className="w-[118px] font-medium">Split</th>
            {states.map(({ type }) => {
              const [mapped, total] = mappedCount(type);
              return (
                <th key={type} className="w-[150px] pr-2 font-medium">
                  <span className="inline-flex items-center gap-1.5">
                    <TypeIcon type={type} />
                    {typeNames[type]}
                    <span
                      data-testid={ids.columnsTypeCount}
                      className={mapped < total ? 'font-semibold text-caution' : undefined}
                      title={`${mapped} of ${total} states are on a column`}
                    >
                      {mapped}/{total}
                    </span>
                  </span>
                </th>
              );
            })}
            <th className="w-[82px] font-medium">Done means</th>
            <th className="w-[30px]" />
          </tr>
        </thead>
        <tbody>
          {columns.map((column, index) => (
            <ColumnRow
              key={column.id}
              column={column}
              states={states}
              fixed={index === 0 || index === last}
              first={index === 0}
              last={index === last}
              readOnly={readOnly}
              dodOpen={dodOpenId === column.id}
              onChange={(next) => onChange(replaceColumn(columns, column.id, next))}
              onToggleDod={() => setDodOpenId(dodOpenId === column.id ? undefined : column.id)}
              onDelete={() => onDelete(column, index)}
              onMove={(step) => onChange(moveColumn(columns, index, index + step))}
              onDragStart={() => setDragFrom(index)}
              onDrop={() => {
                if (dragFrom !== undefined) onChange(moveColumn(columns, dragFrom, index));
                setDragFrom(undefined);
              }}
            />
          ))}
        </tbody>
      </table>
      <footer className="flex items-center gap-2 border-t border-edge bg-canvas px-3 py-1.5 text-[length:var(--text-xs)] text-ink-muted">
        <button
          type="button"
          data-testid={ids.columnsAdd}
          disabled={readOnly}
          className="h-7 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] text-ink disabled:opacity-50"
          onClick={() => onChange(insertBeforeLast(columns, blankColumn(states)))}
        >
          + Add column
        </button>
        New columns go in before the last one. Two columns can share a state. States in the Removed
        category never show on a board.
      </footer>
    </div>
  );
}
