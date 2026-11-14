import { useContext, useEffect, useRef, type KeyboardEvent } from 'react';
import { useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';
import { TouchSearchClose } from './touchSearch';
import type { BacklogSearch } from './useBacklogSearch';

const rowSelector = `[data-testid="${ids.row}"]`;

// The search field in the toolbar. Esc clears it and puts focus back on the list.
export function BacklogSearchBox({ search }: { search: BacklogSearch }) {
  const { focusedId } = useSelection();
  const { attachBox, query, setQuery } = search;
  // Set only by the touch toolbar, which opens this field from ⋯ More and closes it again.
  const closeTouchSearch = useContext(TouchSearchClose);
  const box = useRef<HTMLInputElement | null>(null);
  useEffect(() => {
    if (!closeTouchSearch) return undefined;
    // After the sheet it was chosen from has put focus back where it came from.
    const frame = requestAnimationFrame(() => box.current?.focus());
    return () => cancelAnimationFrame(frame);
  }, [closeTouchSearch]);
  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key !== 'Escape') return;
    event.preventDefault();
    setQuery('');
    // The rows the search left out come back on the next render; focus the one that had focus, else the first.
    requestAnimationFrame(() => {
      const own =
        focusedId === null
          ? null
          : document.querySelector<HTMLElement>(
              `${rowSelector}[data-row-id="${CSS.escape(focusedId)}"]`,
            );
      (own ?? document.querySelector<HTMLElement>(rowSelector))?.focus();
    });
  };
  const input = (
    <input
      ref={(element) => {
        box.current = element;
        attachBox(element);
      }}
      data-testid={ids.searchBox}
      type="search"
      className={
        closeTouchSearch ? 'field min-w-0 flex-1 touch:min-h-[var(--touch-target)]' : 'field w-56'
      }
      placeholder={closeTouchSearch ? 'Search this list' : 'Search this list  ( / )'}
      aria-label="Search this list"
      value={query}
      onChange={(event) => setQuery(event.target.value)}
      onKeyDown={onKeyDown}
    />
  );
  if (!closeTouchSearch) return input;
  return (
    <>
      {input}
      <button
        type="button"
        data-testid={ids.searchCancel}
        className="min-h-[var(--touch-target)] flex-none rounded-control px-3 text-[length:var(--text-base)] font-medium text-signal"
        onClick={() => {
          setQuery('');
          closeTouchSearch();
        }}
      >
        Cancel
      </button>
    </>
  );
}
