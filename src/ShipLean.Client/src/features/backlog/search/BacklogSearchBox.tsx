import type { KeyboardEvent } from 'react';
import { useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';
import type { BacklogSearch } from './useBacklogSearch';

const rowSelector = `[data-testid="${ids.row}"]`;

// The search field in the toolbar. Esc clears it and puts focus back on the list.
export function BacklogSearchBox({ search }: { search: BacklogSearch }) {
  const { focusedId } = useSelection();
  const { attachBox, query, setQuery } = search;
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
  return (
    <input
      ref={(element) => attachBox(element)}
      data-testid={ids.searchBox}
      type="search"
      className="field w-56"
      placeholder="Search this list  ( / )"
      aria-label="Search this list"
      value={query}
      onChange={(event) => setQuery(event.target.value)}
      onKeyDown={onKeyDown}
    />
  );
}
