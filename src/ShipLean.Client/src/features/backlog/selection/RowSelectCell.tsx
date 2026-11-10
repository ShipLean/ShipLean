import type { ChangeEvent } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { backlogTestIds as ids } from '../testIds';
import { useSelection } from './useSelection';

// The checkbox cell at the start of a Stories list or planning row. The box shows on hover or keyboard focus, always
// on touch, and on every row while any row is selected. A click on it selects the row; Shift extends from the anchor.
export function RowSelectCell({ row }: { row: BacklogRowDto }) {
  const selection = useSelection();
  const selected = selection.has(row.id);
  function change(event: ChangeEvent<HTMLInputElement>) {
    selection.setFocused(row.id);
    const native = event.nativeEvent;
    if (native instanceof MouseEvent && native.shiftKey) selection.rangeTo(row.id);
    else selection.toggle(row.id);
  }
  const shown = selected || selection.count > 0;
  return (
    <td data-testid={ids.rowSelect} data-row-id={row.id} className="w-8 px-1 text-center">
      <input
        type="checkbox"
        checked={selected}
        onChange={change}
        aria-label={`Select ${row.key}`}
        tabIndex={-1}
        className={`size-3.5 cursor-pointer accent-[var(--color-accent)] focus-visible:opacity-100 [@media(hover:none)]:opacity-100 [tr:hover_&]:opacity-100 ${shown ? 'opacity-100' : 'opacity-0'}`}
      />
    </td>
  );
}
