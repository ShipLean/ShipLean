import { useState } from 'react';
import { retrosTestIds as ids } from '../../testIds';
import { SearchBox } from '../parts/SearchBox';
import type { RetrosSearchSlotProps } from '../SearchSlotProps';
import { FilterChips } from './FilterChips';
import { FilterSheet } from './FilterSheet';
import { activeFilters } from './searchFilters';

/**
 * The search toolbar at touch (D1, D4): the search field alone on the first row, then Filter (n) and "n points in m
 * retros", then a chip for each active filter. Nothing sticks.
 */
export function SearchTouchToolbar(props: RetrosSearchSlotProps) {
  const { query, search, change } = props;
  const [sheet, setSheet] = useState(false);
  const count = activeFilters(query, search).length;
  const retros = new Set(search.rows.map((row) => row.retroId)).size;
  return (
    <div className="grid gap-2 px-3 pt-2">
      <SearchBox touch value={query.q ?? ''} onWrite={(q) => change({ q })} />
      <div className="flex min-w-0 items-center gap-2">
        <button
          type="button"
          data-testid={ids.retrosFilterOpen}
          data-count={count}
          aria-haspopup="dialog"
          className={`inline-flex min-h-[var(--touch-target)] flex-none cursor-pointer items-center gap-1.5 rounded-control border px-3 text-[length:var(--text-sm)] text-ink ${count > 0 ? 'border-signal bg-signal-soft font-semibold' : 'border-edge bg-panel'}`}
          onClick={() => setSheet(true)}
        >
          Filter
          {count > 0 ? (
            <span className="inline-grid h-[18px] min-w-[18px] place-items-center rounded-full bg-signal px-1.5 text-[length:var(--text-2xs)] text-signal-ink tabular-nums">
              {count}
            </span>
          ) : null}
        </button>
        <span
          data-testid={ids.retrosCount}
          className="ml-auto min-w-0 truncate text-[length:var(--text-sm)] text-ink-muted"
        >
          {search.total} point{search.total === 1 ? '' : 's'} in {retros} retro
          {retros === 1 ? '' : 's'}
        </span>
      </div>
      <FilterChips {...props} />
      <FilterSheet {...props} open={sheet} onClose={() => setSheet(false)} />
    </div>
  );
}
