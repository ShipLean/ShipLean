import { useEffect, useRef } from 'react';
import { useListCommand } from '../commands/listCommands';
import { backlogTestIds as ids } from '../testIds';
import { useSelection } from './useSelection';

// The head cell over the select column: selects every shown row (collapsed sections left out), or clears them when
// they are all selected. It is mounted once per screen, so it also registers the commands the keys and menus run.
export function SelectAllCell() {
  const selection = useSelection();
  const shown = selection.orderedIds;
  const selectedShown = shown.filter((id) => selection.has(id)).length;
  const all = shown.length > 0 && selectedShown === shown.length;
  const box = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (box.current) box.current.indeterminate = selectedShown > 0 && !all;
  }, [selectedShown, all]);
  useListCommand('toggleSelect', (arg) => {
    const id = typeof arg === 'string' ? arg : selection.focusedId;
    if (id !== null) selection.toggle(id);
  });
  useListCommand('clear', () => selection.clear());
  return (
    <th data-testid={ids.selectAll} className="w-8 px-1 text-center">
      <input
        ref={box}
        type="checkbox"
        checked={all}
        disabled={shown.length === 0}
        aria-label="Select all shown rows"
        onChange={() => (all ? selection.removeMany(shown) : selection.addMany(shown))}
        className="size-3.5 cursor-pointer accent-[var(--color-accent)]"
      />
    </th>
  );
}
