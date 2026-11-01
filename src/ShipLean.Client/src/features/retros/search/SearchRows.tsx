import { retrosTestIds as ids } from '../testIds';
import { SearchRow } from './parts/SearchRow';
import { highlightedRow } from './parts/matches';
import type { RetrosSearchSlotProps } from './SearchSlotProps';

const head = 'border-b border-edge px-2.5 py-1.5 text-left font-medium text-ink-muted';

/** One row per point. The clicked point (else the open retro's first row) is the selected one. */
export function SearchRows({ query, search, pointId, select }: RetrosSearchSlotProps) {
  const selectedRow = highlightedRow(search, pointId);
  const logMatches = new Set(search.logMatchPointIds);
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
        <table className="w-full min-w-[46rem] table-fixed border-collapse text-[length:var(--text-sm)]">
          <thead>
            <tr>
              <th className={`${head} w-[118px]`}>Category</th>
              <th className={head}>Point</th>
              <th className={`${head} w-[170px]`}>Theme</th>
              <th className={`${head} w-[170px]`}>Work item</th>
              <th className={`${head} w-[48px]`}>Sprint</th>
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
