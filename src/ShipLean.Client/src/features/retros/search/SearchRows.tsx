import { useTouchShell } from '../../../touch/useTouchShell';
import { retrosTestIds as ids } from '../testIds';
import { SearchRow } from './parts/SearchRow';
import { highlightedRow } from './parts/matches';
import type { RetrosSearchSlotProps } from './SearchSlotProps';
import { PhoneSearchRow } from './touch/PhoneSearchRow';
import { CLEAR_SEARCH_AND_FILTERS, activeFilters } from './touch/searchFilters';

const head = 'border-b border-edge px-2.5 py-1.5 text-left font-medium text-ink-muted';

/** One row per point. The clicked point (else the open retro's first row) is the selected one. */
export function SearchRows({ query, search, pointId, select, change }: RetrosSearchSlotProps) {
  const { touch } = useTouchShell();
  const selectedRow = highlightedRow(search, pointId);
  const logMatches = new Set(search.logMatchPointIds);
  if (touch) {
    const hasFilters = activeFilters(query, search).length > 0;
    const anySet = Boolean(query.q) || hasFilters;
    return (
      <div data-testid={ids.retrosRows} className="min-w-0">
        {search.rows.length === 0 ? (
          <div
            data-testid={ids.retrosEmpty}
            className="grid justify-items-center gap-2 px-3 py-8 text-center text-[length:var(--text-sm)] text-ink-muted"
          >
            <p className="m-0">
              {query.q ? (
                <>
                  No points match <strong className="break-words text-ink">“{query.q}”</strong>
                  {hasFilters ? ' with these filters' : ''}.
                </>
              ) : (
                'No points match these filters.'
              )}
            </p>
            {anySet ? (
              <button
                type="button"
                data-testid={ids.retrosEmptyClear}
                className="btn-ghost min-h-[var(--touch-target)]"
                onClick={() => change(CLEAR_SEARCH_AND_FILTERS)}
              >
                Clear search and filters
              </button>
            ) : null}
          </div>
        ) : (
          <div className="grid border-t border-edge">
            {search.rows.map((row) => (
              <PhoneSearchRow
                key={row.id}
                point={row}
                q={query.q}
                logOnly={logMatches.has(row.id)}
                selected={row === selectedRow}
                onSelect={() => select(row.retroId, row.id)}
              />
            ))}
          </div>
        )}
      </div>
    );
  }
  return (
    <div data-testid={ids.retrosRows} className="min-w-0 overflow-auto">
      {search.rows.length === 0 ? (
        <p
          data-testid={ids.retrosEmpty}
          className="m-0 grid h-20 place-items-center text-[length:var(--text-sm)] text-ink-muted"
        >
          No point matches. Try fewer filters.
        </p>
      ) : (
        <table className="w-full min-w-[36rem] table-fixed border-collapse text-[length:var(--text-sm)]">
          <thead>
            <tr>
              <th className={`${head} w-[108px]`}>Category</th>
              <th className={head}>Point</th>
              <th className={`${head} w-[130px]`}>Theme</th>
              <th className={`${head} w-[100px]`}>Work item</th>
              <th className={`${head} w-[68px]`}>Sprint</th>
              <th className={`${head} w-[40px]`}>By</th>
            </tr>
          </thead>
          <tbody>
            {search.rows.map((row) => (
              <SearchRow
                key={row.id}
                point={row}
                q={query.q}
                logOnly={logMatches.has(row.id)}
                selected={row === selectedRow}
                onSelect={() => select(row.retroId, row.id)}
              />
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
