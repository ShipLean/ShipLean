import { retrosTestIds as ids } from '../../testIds';
import type { RetrosSearchSlotProps } from '../SearchSlotProps';
import { CLEAR_FILTERS, activeFilters } from './searchFilters';

/** D4: a chip for each active filter, ✕ to remove it, and Clear all once two or more are on. Nothing when none is. */
export function FilterChips({ query, search, change }: RetrosSearchSlotProps) {
  const active = activeFilters(query, search);
  if (active.length === 0) return null;
  return (
    <div data-testid={ids.retrosChips} className="flex flex-wrap items-center gap-1.5">
      {active.map((filter) => (
        <span
          key={filter.key}
          data-testid={ids.retrosChip}
          data-filter={filter.key}
          className="inline-flex max-w-full min-h-[var(--touch-target)] items-center gap-1 rounded-full border border-signal bg-signal-soft pr-1 pl-2.5 text-[length:var(--text-sm)] text-ink"
        >
          <span className="min-w-0 truncate">{filter.label}</span>
          <button
            type="button"
            data-testid={ids.retrosChipRemove}
            aria-label={`Remove ${filter.label}`}
            className="grid size-6 flex-none cursor-pointer place-items-center rounded-full text-[length:var(--text-xs)] text-ink-muted"
            onClick={() => change(filter.clear)}
          >
            ✕
          </button>
        </span>
      ))}
      {active.length >= 2 ? (
        <button
          type="button"
          data-testid={ids.retrosChipsClear}
          className="min-h-[var(--touch-target)] cursor-pointer rounded-control px-1.5 text-[length:var(--text-sm)] text-signal"
          onClick={() => change(CLEAR_FILTERS)}
        >
          Clear all
        </button>
      ) : null}
    </div>
  );
}
